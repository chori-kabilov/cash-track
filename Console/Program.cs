using System.Globalization;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Infrastructure.Data;
using Infrastructure.Services;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;
using Console.Bot;
using Domain.Enums;
using Console.Commands;

var _cts = new CancellationTokenSource();
Dictionary<long, UserFlowState> _flow = new();

using var singleInstanceMutex = new Mutex(initiallyOwned: true, name: "Global\\CashTrack.TelegramBot",
    createdNew: out var createdNew);
if (!createdNew)
{
    System.Console.WriteLine("Another bot instance is already running. Exiting.");
    return;
}

// 1. Load Configuration
IConfiguration configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables()
    .Build();

// 2. Data Context
var connectionString = configuration.GetConnectionString("DefaultConnection");
var options = new DbContextOptionsBuilder<DataContext>()
    .UseNpgsql(connectionString)
    .Options;

DataContext dataContext = new DataContext(options);

// 3. Services
UserService userService = new UserService(dataContext);
AccountService accountService = new AccountService(dataContext);
TransactionService transactionService = new TransactionService(dataContext);
CategoryService categoryService = new CategoryService(dataContext);
GoalService goalService = new GoalService(dataContext);
DebtService debtService = new DebtService(dataContext);
RegularPaymentService regularPaymentService = new RegularPaymentService(dataContext);

// 4. Commands
var startCommand = new StartCommand(userService, categoryService);
var helpCommand = new HelpCommand();
var balanceCommand = new BalanceCommand(accountService);
var statsCommand = new StatsCommand(accountService, transactionService, regularPaymentService);
var goalCommand = new GoalCommand(goalService);
var debtCommand = new DebtCommand(debtService);
var regularPaymentCommand = new RegularPaymentCommand(regularPaymentService);

// 5. Bot Token
var botToken = configuration["BotToken"];
if (string.IsNullOrEmpty(botToken))
{
    System.Console.WriteLine("Bot token not found in configuration!");
    return;
}

TelegramBotClient botClient = new TelegramBotClient(botToken);
var me = await botClient.GetMeAsync();
System.Console.WriteLine($"Start listening for @{me.Username}");

// 6. Scheduler
var scheduler = new Console.Services.SchedulerService(botClient, options);
scheduler.Start();

// 7. Start Receiving
var receiverOptions = new ReceiverOptions()
{
    AllowedUpdates = Array.Empty<UpdateType>()
};

botClient.StartReceiving(
    updateHandler: HandleUpdateAsync,
    pollingErrorHandler: HandlePollingErrorAsync,
    receiverOptions: receiverOptions,
    cancellationToken: _cts.Token
);

System.Console.WriteLine("Bot is running. Press Ctrl+C to stop.");

try
{
    await Task.Delay(Timeout.InfiniteTimeSpan, _cts.Token);
}
catch (OperationCanceledException)
{
}

// HANDLERS
async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
{
    if (update.CallbackQuery is { } callbackQuery)
    {
        await HandleCallbackQueryAsync(botClient, callbackQuery, cancellationToken);
        return;
    }

    if (update.Message is not { } message) return;
    if (message.From is not { } from) return;

    var chatId = message.Chat.Id;
    var userId = from.Id;
    var text = (message.Text ?? string.Empty).Trim();

    if (string.IsNullOrWhiteSpace(text)) return;
    System.Console.WriteLine($"Msg: {text} ({chatId})");

    if (text.StartsWith('/'))
    {
        if (string.Equals(text, "/start", StringComparison.OrdinalIgnoreCase))
        {
            await startCommand.ExecuteAsync(botClient, chatId, from, cancellationToken);
            return;
        }
        
        // Handling deep links or special slash commands for items
        if (text.StartsWith("/pay_debt_"))
        {
             if (int.TryParse(text.Substring(10), out var debtId))
             {
                 _flow[userId] = new UserFlowState { Step = UserFlowStep.WaitingDebtPayment, PendingDebtId = debtId };
                 await botClient.SendTextMessageAsync(chatId, "Введите сумму платежа:", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: cancellationToken);
                 return;
             }
        }

        if (text.StartsWith("/pay_regular_"))
        {
             if (int.TryParse(text.Substring(13), out var regId))
             {
                 var payment = await regularPaymentService.GetByIdAsync(userId, regId, cancellationToken);
                 if (payment == null)
                 {
                     await botClient.SendTextMessageAsync(chatId, "❌ Платеж не найден.", replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: cancellationToken);
                     return;
                 }

                 await regularPaymentService.MarkAsPaidAsync(userId, regId, cancellationToken);
                 
                 var catId = payment.CategoryId ?? (await categoryService.GetUserCategoriesAsync(userId, cancellationToken)).FirstOrDefault(c => c.Type == TransactionType.Expense)?.Id;
                 
                 if (catId.HasValue)
                 {
                     await AddTransactionAsync(botClient, chatId, userId, payment.Amount, catId.Value, TransactionType.Expense, $"Регулярный платеж: {payment.Name}", false, cancellationToken);
                 }
                 
                 await botClient.SendTextMessageAsync(chatId, $"✅ Платеж \"{payment.Name}\" отмечен как оплаченный! След: {payment.NextDueDate:dd.MM.yyyy}", replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: cancellationToken);
                 return;
             }
        }

        await SendMainMenuAsync(botClient, chatId, cancellationToken);
        return;
    }

    if (_flow.TryGetValue(userId, out var flow))
    {
        await HandleUserFlowAsync(botClient, chatId, userId, text, flow, cancellationToken);
        return;
    }

    await SendMainMenuAsync(botClient, chatId, cancellationToken);
}

async Task HandleCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
{
    var chatId = callbackQuery.Message?.Chat.Id;
    if (chatId == null) return;

    var userId = callbackQuery.From.Id;
    var data = callbackQuery.Data ?? string.Empty;

    try
    {
        await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);
    }
    catch (ApiRequestException)
    {
        // Игнорируем ошибку, если запрос устарел (например, бот был оффлайн)
    }

    // Global Actions
    if (data == "action:cancel")
    {
        _flow.Remove(userId);
        await SendMainMenuAsync(botClient, chatId.Value, cancellationToken);
        return;
    }

    if (data == "action:skip_desc" || data == "action:toggle_impulsive")
    {
        if (!_flow.TryGetValue(userId, out var flow) || flow.Step != UserFlowStep.WaitingDescription) return;

        if (data == "action:toggle_impulsive")
        {
            flow.PendingIsImpulsive = !flow.PendingIsImpulsive;
            await botClient.EditMessageReplyMarkupAsync(chatId.Value, callbackQuery.Message!.MessageId, replyMarkup: BotInlineKeyboards.SkipDescription(flow.PendingIsImpulsive), cancellationToken: cancellationToken);
            return;
        }
        
        await AddTransactionAsync(botClient, chatId.Value, userId, flow.PendingAmount, flow.PendingCategoryId!.Value, flow.PendingType, null, flow.PendingIsImpulsive, cancellationToken);
        _flow.Remove(userId);
        return;
    }

    // Command Routing
    if (data.StartsWith("menu:", StringComparison.Ordinal))
    {
        var msgId = callbackQuery.Message?.MessageId;
        switch (data)
        {
            case "menu:balance": await balanceCommand.ExecuteAsync(botClient, chatId.Value, userId, cancellationToken, msgId); return;
            case "menu:stats": await statsCommand.ExecuteAsync(botClient, chatId.Value, userId, cancellationToken, msgId); return;
            case "menu:help": await helpCommand.ExecuteAsync(botClient, chatId.Value, cancellationToken, msgId); return;
            case "menu:goals": await goalCommand.ShowMenuAsync(botClient, chatId.Value, userId, cancellationToken, msgId); return;
            case "menu:debts": await debtCommand.ShowMenuAsync(botClient, chatId.Value, userId, cancellationToken, msgId); return;
            case "menu:regular": await regularPaymentCommand.ShowMenuAsync(botClient, chatId.Value, userId, cancellationToken, msgId); return;
            case "menu:limits": 
                await botClient.EditMessageTextAsync(chatId.Value, msgId!.Value, "📉 *Лимиты*\n\nВ разработке! 🚧", ParseMode.Markdown, replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: cancellationToken);
                return;
                
            case "menu:income":
                _flow[userId] = new UserFlowState { Step = UserFlowStep.WaitingAmount, PendingType = TransactionType.Income };
                await botClient.EditMessageTextAsync(chatId.Value, msgId!.Value, "💵 *Доход*\n\nВведите сумму:", ParseMode.Markdown, replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: cancellationToken);
                return;
            case "menu:expense":
                _flow[userId] = new UserFlowState { Step = UserFlowStep.WaitingAmount, PendingType = TransactionType.Expense };
                await botClient.EditMessageTextAsync(chatId.Value, msgId!.Value, "💸 *Расход*\n\nВведите сумму:", ParseMode.Markdown, replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: cancellationToken);
                return;
        }
    }

    // Specific Flow Triggers
    if (data == "regular:create")
    {
         _flow[userId] = new UserFlowState { Step = UserFlowStep.WaitingRegularName };
         await botClient.SendTextMessageAsync(chatId.Value, "Введите название платежа:", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: cancellationToken);
         return;
    }
    if (data.StartsWith("reg:freq:"))
    {
         if (_flow.TryGetValue(userId, out var flow) && flow.Step == UserFlowStep.WaitingRegularFrequency && Enum.TryParse<PaymentFrequency>(data.Split(':')[2], out var freq))
         {
             flow.PendingRegularFrequency = freq;
             flow.Step = UserFlowStep.WaitingRegularDate;
             await botClient.SendTextMessageAsync(chatId.Value, "Введите дату (ДД.ММ.ГГГГ):", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: cancellationToken);
         }
         return;
    }
    if (data == "goal:create")
    {
        _flow[userId] = new UserFlowState { Step = UserFlowStep.WaitingGoalName };
        await botClient.SendTextMessageAsync(chatId.Value, "Введите название цели:", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: cancellationToken);
        return;
    }
    if (data.StartsWith("goal:deposit:") && int.TryParse(data.Split(':')[2], out var goalId))
    {
         _flow[userId] = new UserFlowState { Step = UserFlowStep.WaitingGoalDeposit, PendingGoalId = goalId };
         await botClient.SendTextMessageAsync(chatId.Value, "Введите сумму пополнения:", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: cancellationToken);
         return;
    }
    if (data.StartsWith("debt:create:"))
    {
        var type = data.Split(':')[2] == "i_owe" ? DebtType.IOwe : DebtType.TheyOwe;
        _flow[userId] = new UserFlowState { Step = UserFlowStep.WaitingDebtName, PendingDebtType = type };
        await botClient.SendTextMessageAsync(chatId.Value, type == DebtType.IOwe ? "Кому вы должны?" : "Кто вам должен?", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: cancellationToken);
        return;
    }
    if (data == "cat:new")
    {
         await botClient.EditMessageTextAsync(chatId.Value, callbackQuery.Message!.MessageId, "✏️ *Новая категория*\n\nНапишите название:", ParseMode.Markdown, replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: cancellationToken);
         return;
    }
    if (data.StartsWith("cat:"))
    {
        var parts = data.Split(':');
        if (parts.Length == 3 && int.TryParse(parts[1], out var typeInt) && int.TryParse(parts[2], out var categoryId))
        {
            if (_flow.TryGetValue(userId, out var flow) && flow.Step == UserFlowStep.ChoosingCategory)
            {
                flow.PendingCategoryId = categoryId;
                flow.Step = UserFlowStep.WaitingDescription;
                flow.PendingIsImpulsive = false;
                await botClient.SendTextMessageAsync(chatId.Value, "Введите описание (или пропустить):", replyMarkup: BotInlineKeyboards.SkipDescription(false), cancellationToken: cancellationToken);
            }
            else await SendMainMenuAsync(botClient, chatId.Value, cancellationToken);
            return;
        }
    }
}

async Task HandleUserFlowAsync(ITelegramBotClient botClient, long chatId, long userId, string text, UserFlowState flow, CancellationToken cancellationToken)
{
    if (flow.Step == UserFlowStep.WaitingAmount)
    {
        if (!TryParseAmount(text, out var amount) || amount <= 0)
        {
            await botClient.SendTextMessageAsync(chatId, "❌ Неверная сумма.", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: cancellationToken);
            return;
        }
        
        // Handle "Type text instead of click category" -> New Category
        if (flow.Step == UserFlowStep.ChoosingCategory) // Should never happen unless bug in logic, actually Step is WaitingAmount here.
        { 
            // Logic for "Quick Category" if we were in ChoosingCategory... but we are in WaitingAmount.
        }

        flow.PendingAmount = amount;
        flow.Step = UserFlowStep.ChoosingCategory;
        
        var categories = await GetSuggestedCategoriesAsync(userId, flow.PendingType, cancellationToken);
        await botClient.SendTextMessageAsync(chatId, "Выберите категорию или напишите название новой:", ParseMode.Markdown, replyMarkup: BotInlineKeyboards.Categories(categories, flow.PendingType), cancellationToken: cancellationToken);
        return;
    }

    if (flow.Step == UserFlowStep.ChoosingCategory)
    {
         // User typed text instead of clicking button -> Create New Category
         var newCatName = text.Trim();
         if (newCatName.Length > 20) newCatName = newCatName.Substring(0, 20);
         var newCat = await categoryService.CreateAsync(userId, newCatName, flow.PendingType, "🆕", cancellationToken);
         
         flow.PendingCategoryId = newCat.Id;
         flow.Step = UserFlowStep.WaitingDescription;
         await botClient.SendTextMessageAsync(chatId, $"✅ Категория \"{newCatName}\" создана!\nВведите описание:", replyMarkup: BotInlineKeyboards.SkipDescription(flow.PendingIsImpulsive), cancellationToken: cancellationToken);
         return;
    }

    if (flow.Step == UserFlowStep.WaitingDescription)
    {
        await AddTransactionAsync(botClient, chatId, userId, flow.PendingAmount, flow.PendingCategoryId!.Value, flow.PendingType, text, flow.PendingIsImpulsive, cancellationToken);
        _flow.Remove(userId);
        return;
    }
    
    // Goals
    if (flow.Step == UserFlowStep.WaitingGoalName)
    {
         flow.PendingGoalName = text;
         flow.Step = UserFlowStep.WaitingGoalTarget;
         await botClient.SendTextMessageAsync(chatId, "Введите сумму цели:", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: cancellationToken);
         return;
    }
    if (flow.Step == UserFlowStep.WaitingGoalTarget)
    {
         if (!TryParseAmount(text, out var t) || t <= 0) { await botClient.SendTextMessageAsync(chatId, "❌ Неверная сумма.", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: cancellationToken); return; }
         flow.PendingGoalTarget = t;
         flow.Step = UserFlowStep.WaitingGoalDeadline;
         await botClient.SendTextMessageAsync(chatId, "Введите дедлайн (ДД.ММ.ГГГГ) или 'нет':", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: cancellationToken);
         return;
    }
    if (flow.Step == UserFlowStep.WaitingGoalDeadline)
    {
         DateTimeOffset? dl = null;
         if (DateTime.TryParseExact(text, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) dl = new DateTimeOffset(d, TimeSpan.Zero);
         else if (!text.ToLower().Contains("нет") && !text.ToLower().Contains("no")) { await botClient.SendTextMessageAsync(chatId, "❌ Неверный формат.", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: cancellationToken); return; }
         
         await goalService.CreateAsync(userId, flow.PendingGoalName!, flow.PendingGoalTarget, dl, cancellationToken);
         _flow.Remove(userId);
         await botClient.SendTextMessageAsync(chatId, "✅ Цель создана!", replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: cancellationToken);
         return;
    }
    if (flow.Step == UserFlowStep.WaitingGoalDeposit)
    {
         if (!TryParseAmount(text, out var a) || a <= 0) { await botClient.SendTextMessageAsync(chatId, "❌ Неверная сумма.", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: cancellationToken); return; }
         
         var cats = await categoryService.GetUserCategoriesAsync(userId, cancellationToken);
         var savings = cats.FirstOrDefault(c => c.Name == "Накопления" && c.Type == TransactionType.Expense) ?? cats.FirstOrDefault(c => c.Type == TransactionType.Expense);
         if (savings != null) await AddTransactionAsync(botClient, chatId, userId, a, savings.Id, TransactionType.Expense, "Пополнение цели", false, cancellationToken);
         
         await goalService.AddFundsAsync(userId, flow.PendingGoalId!.Value, a, cancellationToken);
         _flow.Remove(userId);
         
         var goal = (await goalService.GetUserGoalsAsync(userId, cancellationToken)).FirstOrDefault(g => g.Id == flow.PendingGoalId);
         var msg = $"✅ Пополнено на {a:F2}!";
         if (goal?.IsCompleted == true) msg += $"\n🎉 Цель \"{goal.Name}\" достигнута!";
         await botClient.SendTextMessageAsync(chatId, msg, replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: cancellationToken);
         return;
    }

    // Debts
    if (flow.Step == UserFlowStep.WaitingDebtName)
    {
         flow.PendingDebtName = text;
         flow.Step = UserFlowStep.WaitingDebtAmount;
         await botClient.SendTextMessageAsync(chatId, "Введите сумму долга:", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: cancellationToken);
         return;
    }
    if (flow.Step == UserFlowStep.WaitingDebtAmount)
    {
         if (!TryParseAmount(text, out var a) || a <= 0) { await botClient.SendTextMessageAsync(chatId, "❌ Неверная сумма.", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: cancellationToken); return; }
         flow.PendingDebtAmount = a;
         flow.Step = UserFlowStep.WaitingDebtDeadline;
         await botClient.SendTextMessageAsync(chatId, "Срок возврата (ДД.ММ.ГГГГ) или 'нет':", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: cancellationToken);
         return;
    }
    if (flow.Step == UserFlowStep.WaitingDebtDeadline)
    {
         DateTimeOffset? dl = null;
         if (DateTime.TryParseExact(text, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) dl = new DateTimeOffset(d, TimeSpan.Zero);
         
         await debtService.CreateAsync(userId, flow.PendingDebtName!, flow.PendingDebtAmount, flow.PendingDebtType, null, dl, cancellationToken);
         _flow.Remove(userId);
         await botClient.SendTextMessageAsync(chatId, "✅ Долг записан!", replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: cancellationToken);
         return;
    }
    if (flow.Step == UserFlowStep.WaitingDebtPayment)
    {
         if (!TryParseAmount(text, out var a) || a <= 0) { await botClient.SendTextMessageAsync(chatId, "❌ Неверная сумма.", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: cancellationToken); return; }
         await debtService.MakePaymentAsync(userId, flow.PendingDebtId!.Value, a, cancellationToken);
         
         var debt = await debtService.GetByIdAsync(userId, flow.PendingDebtId!.Value, cancellationToken);
         if (debt != null)
         {
             var cats = await categoryService.GetUserCategoriesAsync(userId, cancellationToken);
             var catName = "Долги";
             if (debt.Type == DebtType.IOwe)
             {
                 var c = cats.FirstOrDefault(x => x.Name == catName) ?? cats.FirstOrDefault(x => x.Type == TransactionType.Expense);
                 if (c != null) await AddTransactionAsync(botClient, chatId, userId, a, c.Id, TransactionType.Expense, $"Возврат: {debt.PersonName}", false, cancellationToken);
             }
             else
             {
                 var c = cats.FirstOrDefault(x => x.Name == catName) ?? cats.FirstOrDefault(x => x.Type == TransactionType.Income);
                 if (c != null) await AddTransactionAsync(botClient, chatId, userId, a, c.Id, TransactionType.Income, $"Возврат: {debt.PersonName}", false, cancellationToken);
             }
         }
         _flow.Remove(userId);
         await botClient.SendTextMessageAsync(chatId, "✅ Платёж учтён!", replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: cancellationToken);
         return;
    }
    
    // Regular
    if (flow.Step == UserFlowStep.WaitingRegularName)
    {
         flow.PendingRegularName = text;
         flow.Step = UserFlowStep.WaitingRegularAmount;
         await botClient.SendTextMessageAsync(chatId, "Введите сумму:", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: cancellationToken);
         return;
    }
    if (flow.Step == UserFlowStep.WaitingRegularAmount)
    {
         if (!TryParseAmount(text, out var a) || a <= 0) { await botClient.SendTextMessageAsync(chatId, "❌ Неверная сумма.", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: cancellationToken); return; }
         flow.PendingRegularAmount = a;
         flow.Step = UserFlowStep.WaitingRegularFrequency;
         await botClient.SendTextMessageAsync(chatId, "Как часто?", replyMarkup: new InlineKeyboardMarkup(new[]{new[]{InlineKeyboardButton.WithCallbackData("Ежедневно", "reg:freq:Daily"), InlineKeyboardButton.WithCallbackData("Еженедельно", "reg:freq:Weekly")}, new[]{InlineKeyboardButton.WithCallbackData("Ежемесячно", "reg:freq:Monthly"), InlineKeyboardButton.WithCallbackData("Ежегодно", "reg:freq:Yearly")}}), cancellationToken: cancellationToken);
         return;
    }
    if (flow.Step == UserFlowStep.WaitingRegularDate)
    {
         if (DateTime.TryParseExact(text, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) 
         {
             await regularPaymentService.CreateAsync(userId, flow.PendingRegularName!, flow.PendingRegularAmount, flow.PendingRegularFrequency, null, null, 3, new DateTimeOffset(d, TimeSpan.Zero), cancellationToken);
             _flow.Remove(userId);
             await botClient.SendTextMessageAsync(chatId, "✅ Платеж создан!", replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: cancellationToken);
         }
         else await botClient.SendTextMessageAsync(chatId, "❌ Неверная дата.", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: cancellationToken);
         return;
    }
}


// HELPERS
Task SendMainMenuAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
{
    return botClient.SendTextMessageAsync(chatId, "Выберите действие:", replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: cancellationToken);
}

async Task AddTransactionAsync(ITelegramBotClient botClient, long chatId, long userId, decimal amount, int categoryId, TransactionType type, string? description, bool isImpulsive, CancellationToken cancellationToken)
{
    try
    {
        var txn = await transactionService.ProcessTransactionAsync(userId, categoryId, amount, type, description, isImpulsive, null, cancellationToken);
        var account = await accountService.GetUserAccountAsync(userId, cancellationToken);
        var category = await categoryService.GetCategoryByIdAsync(userId, categoryId, cancellationToken);
        
        var sign = type == TransactionType.Income ? "+" : "-";
        var emoji = type == TransactionType.Income ? "✅" : "🛍️";
        var catName = category != null ? $"{category.Name} {category.Icon}" : "";
        var desc = !string.IsNullOrEmpty(description) ? $"\n📝 *{description}*" : "";
        var imp = isImpulsive ? "\n⚡ На эмоциях" : "";
        
        await botClient.SendTextMessageAsync(chatId, 
            $"{emoji} *{sign}{amount:F2} {account?.Currency}*\n📂 *{catName}*{desc}{imp}\n\n💰 Баланс: *{account?.Balance:F2}*", 
            ParseMode.Markdown, replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: cancellationToken);
    }
    catch (Exception ex)
    {
        System.Console.WriteLine(ex);
        await botClient.SendTextMessageAsync(chatId, "❌ Ошибка: " + ex.Message, replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: cancellationToken);
    }
}

async Task<IReadOnlyList<Domain.Entities.Category>> GetSuggestedCategoriesAsync(long userId, TransactionType type, CancellationToken cancellationToken)
{
    var recentIds = await transactionService.GetRecentCategoryIdsAsync(userId, type, 6, cancellationToken);
    var all = await categoryService.GetUserCategoriesAsync(userId, cancellationToken);
    if (!all.Any()) 
    {
        await categoryService.InitializeDefaultCategoriesAsync(userId, cancellationToken);
        all = await categoryService.GetUserCategoriesAsync(userId, cancellationToken);
    }
    var relevant = all.Where(c => c.Type == type).ToList();
    var result = new List<Domain.Entities.Category>();
    foreach (var id in recentIds) { var c = relevant.FirstOrDefault(x => x.Id == id); if (c != null) result.Add(c); }
    foreach (var c in relevant.OrderBy(x => x.Priority)) { if (!result.Contains(c)) result.Add(c); if (result.Count >= 9) break; }
    return result;
}

bool TryParseAmount(string t, out decimal a) => decimal.TryParse(t.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out a);

Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken) { System.Console.WriteLine(exception); return Task.CompletedTask; }
