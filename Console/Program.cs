using System.Globalization;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Infrastructure.Data;
using Infrastructure.Services;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Console.Bot;
using Domain.Enums;
using Console.Commands;
using Console.Flow;

// ГЛОБАЛЬНОЕ СОСТОЯНИЕ
var _cts = new CancellationTokenSource();
Dictionary<long, UserFlowState> _flow = new(); // Состояние диалога каждого пользователя

// ЗАЩИТА ОТ ДУБЛЕЙ
// Только один экземпляр бота может работать одновременно
using var mutex = new Mutex(true, "Global\\CashTrack.TelegramBot", out var isNewInstance);
if (!isNewInstance)
{
    System.Console.WriteLine("Бот уже запущен. Выход.");
    return;
}

// 1. КОНФИГУРАЦИЯ
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

// 2. БАЗА ДАННЫХ
var dbOptions = new DbContextOptionsBuilder<DataContext>()
    .UseNpgsql(config.GetConnectionString("DefaultConnection"))
    .Options;
var db = new DataContext(dbOptions);

// 3. СЕРВИСЫ
var userService = new UserService(db);
var accountService = new AccountService(db);
var transactionService = new TransactionService(db);
var categoryService = new CategoryService(db);
var goalService = new GoalService(db);
var debtService = new DebtService(db);
var regularService = new RegularPaymentService(db);

// 4. ОБРАБОТЧИКИ КОМАНД
var startCmd = new StartCommand(userService, categoryService);
var helpCmd = new HelpCommand();
var balanceCmd = new BalanceCommand(accountService);
var statsCmd = new StatsCommand(accountService, transactionService, regularService);
var goalCmd = new GoalCommand(goalService);
var debtCmd = new DebtCommand(debtService);
var regularCmd = new RegularPaymentCommand(regularService);

// 5. ОБРАБОТЧИК ДИАЛОГОВ
var flowHandler = new FlowHandler(categoryService, goalService, debtService, regularService, transactionService, accountService);

// 6. TELEGRAM BOT
var botToken = config["BotToken"] ?? throw new Exception("BotToken не найден в конфигурации!");
var bot = new TelegramBotClient(botToken);
var me = await bot.GetMeAsync();
System.Console.WriteLine($"Бот @{me.Username} запущен");

// 7. ФОНОВЫЙ ПЛАНИРОВЩИК
var scheduler = new Console.Services.SchedulerService(bot, dbOptions);
scheduler.Start();

// 8. ЗАПУСК POLLING
bot.StartReceiving(
    updateHandler: HandleUpdateAsync,
    pollingErrorHandler: (_, ex, _) => { System.Console.WriteLine(ex); return Task.CompletedTask; },
    receiverOptions: new ReceiverOptions { AllowedUpdates = [] },
    cancellationToken: _cts.Token);

System.Console.WriteLine("Нажмите Ctrl+C для остановки.");
try { await Task.Delay(Timeout.InfiniteTimeSpan, _cts.Token); }
catch (OperationCanceledException) { }

// ОБРАБОТЧИКИ

// Главный обработчик всех входящих обновлений
async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
{
    try
    {
        // Обработка нажатий на inline-кнопки
        if (update.CallbackQuery is { } cb)
        {
            await HandleCallbackAsync(botClient, cb, ct);
            return;
        }

        // Обработка текстовых сообщений
        if (update.Message is not { Text: { } text } msg || msg.From is null) return;
        
        var chatId = msg.Chat.Id;
        var userId = msg.From.Id;
        text = text.Trim();
        
        if (string.IsNullOrEmpty(text)) return;
        System.Console.WriteLine($"[{userId}] {text}");

        // Команды (начинаются с /)
        if (text.StartsWith('/'))
        {
            await HandleCommandAsync(botClient, chatId, userId, text, msg.From, ct);
            return;
        }

        // Диалоговый поток (если пользователь в процессе ввода)
        if (_flow.TryGetValue(userId, out var flow))
        {
            await flowHandler.HandleAsync(botClient, chatId, userId, text, flow, _flow, ct);
            return;
        }

        // По умолчанию — главное меню
        await SendMenuAsync(botClient, chatId, ct);
    }
    catch (Exception ex)
    {
        System.Console.WriteLine($"Ошибка: {ex}");
    }
}

// Обработка команд (/)
async Task HandleCommandAsync(ITelegramBotClient botClient, long chatId, long userId, string text, User from, CancellationToken ct)
{
    if (text.Equals("/start", StringComparison.OrdinalIgnoreCase))
    {
        await startCmd.ExecuteAsync(botClient, chatId, from, ct);
        return;
    }

    // /pay_debt_123 — оплата долга
    if (text.StartsWith("/pay_debt_") && int.TryParse(text[10..], out var debtId))
    {
        _flow[userId] = new UserFlowState { Step = UserFlowStep.WaitingDebtPayment, PendingDebtId = debtId };
        await botClient.SendTextMessageAsync(chatId, "Введите сумму платежа:", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
        return;
    }

    // /pay_regular_123 — оплата регулярного платежа
    if (text.StartsWith("/pay_regular_") && int.TryParse(text[13..], out var regId))
    {
        var payment = await regularService.GetByIdAsync(userId, regId, ct);
        if (payment == null)
        {
            await botClient.SendTextMessageAsync(chatId, "❌ Платеж не найден.", replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
            return;
        }

        await regularService.MarkAsPaidAsync(userId, regId, ct);
        var catId = payment.CategoryId ?? (await categoryService.GetUserCategoriesAsync(userId, ct)).FirstOrDefault(c => c.Type == TransactionType.Expense)?.Id;
        
        if (catId.HasValue)
            await flowHandler.AddTransactionAsync(botClient, chatId, userId, payment.Amount, catId.Value, TransactionType.Expense, $"Регулярный: {payment.Name}", false, ct);

        await botClient.SendTextMessageAsync(chatId, $"✅ \"{payment.Name}\" оплачен! След: {payment.NextDueDate:dd.MM}", replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
        return;
    }

    await SendMenuAsync(botClient, chatId, ct);
}

// Обработка нажатий на inline-кнопки
async Task HandleCallbackAsync(ITelegramBotClient botClient, CallbackQuery cb, CancellationToken ct)
{
    var chatId = cb.Message?.Chat.Id;
    if (chatId == null) return;

    var userId = cb.From.Id;
    var data = cb.Data ?? "";
    var msgId = cb.Message?.MessageId;

    // Подтверждаем получение callback (убирает "часики" на кнопке)
    try { await botClient.AnswerCallbackQueryAsync(cb.Id, cancellationToken: ct); }
    catch (ApiRequestException) { /* Игнорируем устаревшие запросы */ }

    // ГЛОБАЛЬНЫЕ ДЕЙСТВИЯ
    
    if (data == "action:cancel")
    {
        _flow.Remove(userId);
        await SendMenuAsync(botClient, chatId.Value, ct);
        return;
    }

    if (data == "action:skip_desc" && _flow.TryGetValue(userId, out var skipFlow) && skipFlow.Step == UserFlowStep.WaitingDescription)
    {
        await flowHandler.AddTransactionAsync(botClient, chatId.Value, userId, skipFlow.PendingAmount, skipFlow.PendingCategoryId!.Value, skipFlow.PendingType, null, skipFlow.PendingIsImpulsive, ct);
        _flow.Remove(userId);
        return;
    }

    if (data == "action:toggle_impulsive" && _flow.TryGetValue(userId, out var impFlow) && impFlow.Step == UserFlowStep.WaitingDescription)
    {
        impFlow.PendingIsImpulsive = !impFlow.PendingIsImpulsive;
        await botClient.EditMessageReplyMarkupAsync(chatId.Value, msgId!.Value, replyMarkup: BotInlineKeyboards.SkipDescription(impFlow.PendingIsImpulsive), cancellationToken: ct);
        return;
    }

    // МЕНЮ
    
    if (data.StartsWith("menu:"))
    {
        switch (data)
        {
            case "menu:balance": await balanceCmd.ExecuteAsync(botClient, chatId.Value, userId, ct, msgId); return;
            case "menu:stats": await statsCmd.ExecuteAsync(botClient, chatId.Value, userId, ct, msgId); return;
            case "menu:help": await helpCmd.ExecuteAsync(botClient, chatId.Value, ct, msgId); return;
            case "menu:goals": await goalCmd.ShowMenuAsync(botClient, chatId.Value, userId, ct, msgId); return;
            case "menu:debts": await debtCmd.ShowMenuAsync(botClient, chatId.Value, userId, ct, msgId); return;
            case "menu:regular": await regularCmd.ShowMenuAsync(botClient, chatId.Value, userId, ct, msgId); return;
            case "menu:limits":
                await botClient.EditMessageTextAsync(chatId.Value, msgId!.Value, "📉 *Лимиты*\n\n🚧 В разработке!", ParseMode.Markdown, replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
                return;
            case "menu:income":
                _flow[userId] = new UserFlowState { Step = UserFlowStep.WaitingAmount, PendingType = TransactionType.Income };
                await botClient.EditMessageTextAsync(chatId.Value, msgId!.Value, "💵 *Доход*\n\nВведите сумму:", ParseMode.Markdown, replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
                return;
            case "menu:expense":
                _flow[userId] = new UserFlowState { Step = UserFlowStep.WaitingAmount, PendingType = TransactionType.Expense };
                await botClient.EditMessageTextAsync(chatId.Value, msgId!.Value, "💸 *Расход*\n\nВведите сумму:", ParseMode.Markdown, replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
                return;
        }
    }

    // СОЗДАНИЕ СУЩНОСТЕЙ
    
    if (data == "regular:create")
    {
        _flow[userId] = new UserFlowState { Step = UserFlowStep.WaitingRegularName };
        await botClient.SendTextMessageAsync(chatId.Value, "Введите название платежа:", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
        return;
    }

    if (data.StartsWith("reg:freq:") && _flow.TryGetValue(userId, out var regFlow) && regFlow.Step == UserFlowStep.WaitingRegularFrequency)
    {
        if (Enum.TryParse<PaymentFrequency>(data.Split(':')[2], out var freq))
        {
            regFlow.PendingRegularFrequency = freq;
            regFlow.Step = UserFlowStep.WaitingRegularDate;
            await botClient.SendTextMessageAsync(chatId.Value, "Введите дату (ДД.ММ.ГГГГ):", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
        }
        return;
    }

    if (data == "goal:create")
    {
        _flow[userId] = new UserFlowState { Step = UserFlowStep.WaitingGoalName };
        await botClient.SendTextMessageAsync(chatId.Value, "Введите название цели:", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
        return;
    }

    if (data.StartsWith("goal:deposit:") && int.TryParse(data.Split(':')[2], out var goalId))
    {
        _flow[userId] = new UserFlowState { Step = UserFlowStep.WaitingGoalDeposit, PendingGoalId = goalId };
        await botClient.SendTextMessageAsync(chatId.Value, "Введите сумму пополнения:", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
        return;
    }

    if (data.StartsWith("debt:create:"))
    {
        var type = data.Split(':')[2] == "i_owe" ? DebtType.IOwe : DebtType.TheyOwe;
        _flow[userId] = new UserFlowState { Step = UserFlowStep.WaitingDebtName, PendingDebtType = type };
        await botClient.SendTextMessageAsync(chatId.Value, type == DebtType.IOwe ? "Кому вы должны?" : "Кто вам должен?", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
        return;
    }

    // ВЫБОР КАТЕГОРИИ
    
    if (data == "cat:new")
    {
        await botClient.EditMessageTextAsync(chatId.Value, msgId!.Value, "✏️ *Новая категория*\n\nНапишите название:", ParseMode.Markdown, replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
        return;
    }

    if (data.StartsWith("cat:"))
    {
        var parts = data.Split(':');
        if (parts.Length == 3 && int.TryParse(parts[2], out var catId) && _flow.TryGetValue(userId, out var catFlow) && catFlow.Step == UserFlowStep.ChoosingCategory)
        {
            catFlow.PendingCategoryId = catId;
            catFlow.Step = UserFlowStep.WaitingDescription;
            catFlow.PendingIsImpulsive = false;
            await botClient.SendTextMessageAsync(chatId.Value, "Введите описание (или пропустить):", replyMarkup: BotInlineKeyboards.SkipDescription(false), cancellationToken: ct);
            return;
        }
        await SendMenuAsync(botClient, chatId.Value, ct);
    }
}

// ХЕЛПЕРЫ

Task SendMenuAsync(ITelegramBotClient botClient, long chatId, CancellationToken ct) =>
    botClient.SendTextMessageAsync(chatId, "Выберите действие:", replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
