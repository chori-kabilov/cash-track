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
using Telegram.Bot.Types.InputFiles;
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
var balanceCmd = new BalanceCommand(accountService, goalService, debtService, regularService, transactionService);
var limitService = new LimitService(db);
var statsCmd = new StatsCommand(accountService, transactionService, categoryService, limitService, regularService);
var goalCmd = new GoalCommand(goalService);
var debtCmd = new DebtCommand(debtService);
var regularCmd = new RegularPaymentCommand(regularService);
var limitCmd = new LimitCommand(limitService, categoryService);

// 5. ОБРАБОТЧИК ДИАЛОГОВ
var flowHandler = new FlowHandler(categoryService, goalService, debtService, regularService, transactionService, accountService, limitService);

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

    // Переключение флага "На эмоциях" для расхода
    if (data == "action:toggle_impulsive" && _flow.TryGetValue(userId, out var impFlow) && impFlow.Step == UserFlowStep.WaitingAmount && impFlow.PendingType == TransactionType.Expense)
    {
        impFlow.PendingIsImpulsive = !impFlow.PendingIsImpulsive;
        await botClient.EditMessageReplyMarkupAsync(chatId.Value, msgId!.Value, replyMarkup: BotInlineKeyboards.ExpenseStart(impFlow.PendingIsImpulsive), cancellationToken: ct);
        return;
    }

    // МЕНЮ
    
    if (data.StartsWith("menu:"))
    {
        switch (data)
        {
            case "menu:balance": 
                _flow.TryGetValue(userId, out var balFlow);
                if (balFlow == null) { balFlow = new UserFlowState(); _flow[userId] = balFlow; }
                await balanceCmd.ExecuteAsync(botClient, chatId.Value, userId, balFlow, ct, msgId); 
                return;
            case "menu:stats": 
                _flow.TryGetValue(userId, out var statFlow);
                if (statFlow == null) { statFlow = new UserFlowState(); _flow[userId] = statFlow; }
                await statsCmd.ExecuteAsync(botClient, chatId.Value, userId, statFlow, ct, msgId); 
                return;
            case "menu:help": await helpCmd.ExecuteAsync(botClient, chatId.Value, ct, msgId); return;
            case "menu:goals": await goalCmd.ShowMenuAsync(botClient, chatId.Value, userId, ct, msgId); return;
            case "menu:debts": await debtCmd.ShowMenuAsync(botClient, chatId.Value, userId, ct, msgId); return;
            case "menu:regular": await regularCmd.ShowMenuAsync(botClient, chatId.Value, userId, ct, msgId); return;
            case "menu:limits": await limitCmd.ShowMenuAsync(botClient, chatId.Value, userId, ct, msgId); return;
            case "menu:income":
                _flow[userId] = new UserFlowState { Step = UserFlowStep.WaitingAmount, PendingType = TransactionType.Income, PendingMessageId = msgId };
                await botClient.EditMessageTextAsync(chatId.Value, msgId!.Value, 
                    "💵 *Доход*\n\nВведите сумму и описание через пробел:\n_Пример: 5000 премия_", 
                    ParseMode.Markdown, replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
                return;
            case "menu:expense":
                _flow[userId] = new UserFlowState { Step = UserFlowStep.WaitingAmount, PendingType = TransactionType.Expense, PendingMessageId = msgId, PendingIsImpulsive = false };
                await botClient.EditMessageTextAsync(chatId.Value, msgId!.Value, 
                    "💸 *Расход*\n\nВведите сумму и описание через пробел:\n_Пример: 150 такси_", 
                    ParseMode.Markdown, replyMarkup: BotInlineKeyboards.ExpenseStart(false), cancellationToken: ct);
                return;
        }
    }

    // === БАЛАНС: ПЕРЕКЛЮЧАТЕЛИ ===
    
    if (data.StartsWith("bal:toggle_") && _flow.TryGetValue(userId, out var toggleFlow))
    {
        switch (data)
        {
            case "bal:toggle_debts": toggleFlow.BalanceShowDebts = !toggleFlow.BalanceShowDebts; break;
            case "bal:toggle_goals": toggleFlow.BalanceShowGoals = !toggleFlow.BalanceShowGoals; break;
            case "bal:toggle_payments": toggleFlow.BalanceShowPayments = !toggleFlow.BalanceShowPayments; break;
        }
        await balanceCmd.ExecuteAsync(botClient, chatId.Value, userId, toggleFlow, ct, msgId);
        return;
    }
    
    if (data == "bal:back")
    {
        _flow.Remove(userId);
        await botClient.EditMessageTextAsync(chatId.Value, msgId!.Value, "Выберите действие:", 
            replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
        return;
    }
    
    if (data == "bal:details" && _flow.TryGetValue(userId, out var detailFlow))
    {
        var transactions = await transactionService.GetUserTransactionsAsync(userId, 10, ct);
        var lines = transactions.Select(t => 
            $"{(t.Type == TransactionType.Income ? "+" : "-")}{t.Amount:F0} {t.Category?.Icon} {t.Description ?? t.Category?.Name}");
        var text = "📊 *Последние операции:*\n\n" + string.Join("\n", lines);
        
        // Только кнопка "Назад" к балансу
        await botClient.EditMessageTextAsync(chatId.Value, msgId!.Value, text,
            ParseMode.Markdown, replyMarkup: BotInlineKeyboards.BalanceDetails(), cancellationToken: ct);
        return;
    }
    
    if (data == "bal:back_to_dashboard" && _flow.TryGetValue(userId, out var backDashFlow))
    {
        await balanceCmd.ExecuteAsync(botClient, chatId.Value, userId, backDashFlow, ct, msgId);
        return;
    }

    // === СТАТИСТИКА: НАВИГАЦИЯ ===
    
    if (data.StartsWith("stat:") && _flow.TryGetValue(userId, out var sFlow))
    {
        switch (data)
        {
            case "stat:summary":
                sFlow.CurrentStatsScreen = StatsScreen.Summary;
                await statsCmd.RenderCurrentScreenAsync(botClient, chatId.Value, userId, sFlow, ct, msgId);
                return;
            case "stat:categories":
                sFlow.CurrentStatsScreen = StatsScreen.Categories;
                await statsCmd.RenderCurrentScreenAsync(botClient, chatId.Value, userId, sFlow, ct, msgId);
                return;
            case "stat:history":
                sFlow.CurrentStatsScreen = StatsScreen.History;
                sFlow.StatsPage = 1;
                await statsCmd.RenderCurrentScreenAsync(botClient, chatId.Value, userId, sFlow, ct, msgId);
                return;
            case "stat:emotions":
                sFlow.CurrentStatsScreen = StatsScreen.Emotions;
                await statsCmd.RenderCurrentScreenAsync(botClient, chatId.Value, userId, sFlow, ct, msgId);
                return;
            case "stat:regular":
                sFlow.CurrentStatsScreen = StatsScreen.Regular;
                await statsCmd.RenderCurrentScreenAsync(botClient, chatId.Value, userId, sFlow, ct, msgId);
                return;
            case "stat:period":
                sFlow.CurrentStatsScreen = StatsScreen.PeriodSelect;
                await statsCmd.RenderCurrentScreenAsync(botClient, chatId.Value, userId, sFlow, ct, msgId);
                return;
            case "stat:prev":
                sFlow.StatsDate = sFlow.StatsPeriod switch
                {
                    StatsPeriod.Week => sFlow.StatsDate.AddDays(-7),
                    StatsPeriod.Month => sFlow.StatsDate.AddMonths(-1),
                    StatsPeriod.Year => sFlow.StatsDate.AddYears(-1),
                    _ => sFlow.StatsDate.AddMonths(-1)
                };
                await statsCmd.RenderCurrentScreenAsync(botClient, chatId.Value, userId, sFlow, ct, msgId);
                return;
            case "stat:next":
                sFlow.StatsDate = sFlow.StatsPeriod switch
                {
                    StatsPeriod.Week => sFlow.StatsDate.AddDays(7),
                    StatsPeriod.Month => sFlow.StatsDate.AddMonths(1),
                    StatsPeriod.Year => sFlow.StatsDate.AddYears(1),
                    _ => sFlow.StatsDate.AddMonths(1)
                };
                await statsCmd.RenderCurrentScreenAsync(botClient, chatId.Value, userId, sFlow, ct, msgId);
                return;
            case "stat:period:week":
                sFlow.StatsPeriod = StatsPeriod.Week;
                sFlow.CurrentStatsScreen = StatsScreen.Summary;
                await statsCmd.RenderCurrentScreenAsync(botClient, chatId.Value, userId, sFlow, ct, msgId);
                return;
            case "stat:period:month":
                sFlow.StatsPeriod = StatsPeriod.Month;
                sFlow.CurrentStatsScreen = StatsScreen.Summary;
                await statsCmd.RenderCurrentScreenAsync(botClient, chatId.Value, userId, sFlow, ct, msgId);
                return;
            case "stat:period:year":
                sFlow.StatsPeriod = StatsPeriod.Year;
                sFlow.CurrentStatsScreen = StatsScreen.Summary;
                await statsCmd.RenderCurrentScreenAsync(botClient, chatId.Value, userId, sFlow, ct, msgId);
                return;
            case "stat:cat:exp":
                sFlow.StatsShowExpenses = true;
                await statsCmd.RenderCurrentScreenAsync(botClient, chatId.Value, userId, sFlow, ct, msgId);
                return;
            case "stat:cat:inc":
                sFlow.StatsShowExpenses = false;
                await statsCmd.RenderCurrentScreenAsync(botClient, chatId.Value, userId, sFlow, ct, msgId);
                return;
            case "stat:page:prev":
                if (sFlow.StatsPage > 1) sFlow.StatsPage--;
                await statsCmd.RenderCurrentScreenAsync(botClient, chatId.Value, userId, sFlow, ct, msgId);
                return;
            case "stat:page:next":
                sFlow.StatsPage++;
                await statsCmd.RenderCurrentScreenAsync(botClient, chatId.Value, userId, sFlow, ct, msgId);
                return;
            case "stat:back":
                _flow.Remove(userId);
                await botClient.EditMessageTextAsync(chatId.Value, msgId!.Value, "Выберите действие:",
                    replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
                return;
            case "stat:export":
                // CSV Export
                var csv = await GenerateCsvAsync(userId, sFlow, transactionService, ct);
                using (var stream = new System.IO.MemoryStream(System.Text.Encoding.UTF8.GetBytes(csv)))
                {
                    var fileName = $"CashTrack_{sFlow.StatsDate:yyyy_MM}.csv";
                    await botClient.SendDocumentAsync(chatId.Value, 
                        new InputOnlineFile(stream, fileName), 
                        caption: "📄 Ваш финансовый отчет", cancellationToken: ct);
                }
                return;
            case "stat:noop":
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
    
    // "Другое" — редактируем сообщение для ввода названия
    if (data == "cat:new" && _flow.TryGetValue(userId, out var newCatFlow))
    {
        newCatFlow.Step = UserFlowStep.WaitingNewCategory;
        await botClient.EditMessageTextAsync(chatId.Value, msgId!.Value, 
            "🆕 *Новый источник?*\n\nВведите название:", 
            ParseMode.Markdown, replyMarkup: BotInlineKeyboards.NewCategoryInput(), cancellationToken: ct);
        return;
    }

    // Выбор существующей категории — сразу записываем транзакцию
    if (data.StartsWith("cat:"))
    {
        var parts = data.Split(':');
        if (parts.Length == 3 && int.TryParse(parts[2], out var catId) && _flow.TryGetValue(userId, out var catFlow) && catFlow.Step == UserFlowStep.ChoosingCategory)
        {
            catFlow.PendingCategoryId = catId;
            
            // Записываем транзакцию и показываем результат
            var (txnId, resultMsgId) = await flowHandler.AddTransactionAsync(botClient, chatId.Value, userId, catFlow, ct);
            if (txnId.HasValue)
            {
                catFlow.PendingTransactionId = txnId;
                catFlow.PendingMessageId = resultMsgId;
                catFlow.Step = UserFlowStep.None;
            }
            else
            {
                _flow.Remove(userId);
            }
            return;
        }
        await SendMenuAsync(botClient, chatId.Value, ct);
    }

    // === НАВИГАЦИЯ "НАЗАД" ===
    
    // Назад к вводу суммы (из категорий)
    if (data == "back:amount" && _flow.TryGetValue(userId, out var backAmountFlow))
    {
        backAmountFlow.Step = UserFlowStep.WaitingAmount;
        var keyboard = backAmountFlow.PendingType == TransactionType.Expense 
            ? BotInlineKeyboards.ExpenseStart(backAmountFlow.PendingIsImpulsive) 
            : BotInlineKeyboards.Cancel();
        var emoji = backAmountFlow.PendingType == TransactionType.Expense ? "💸" : "💵";
        var typeName = backAmountFlow.PendingType == TransactionType.Expense ? "Расход" : "Доход";
        
        await botClient.EditMessageTextAsync(chatId.Value, msgId!.Value,
            $"{emoji} *{typeName}*\n\nВведите сумму и описание через пробел:",
            ParseMode.Markdown, replyMarkup: keyboard, cancellationToken: ct);
        return;
    }
    
    // Назад к категориям (из ввода новой категории)
    if (data == "back:categories" && _flow.TryGetValue(userId, out var backCatFlow))
    {
        backCatFlow.Step = UserFlowStep.ChoosingCategory;
        var categories = await flowHandler.GetSuggestedCategoriesAsync(userId, backCatFlow.PendingType, ct);
        var prompt = backCatFlow.PendingType == TransactionType.Income ? "Откуда доход?" : "Выберите категорию:";
        
        await botClient.EditMessageTextAsync(chatId.Value, msgId!.Value, prompt,
            replyMarkup: BotInlineKeyboards.CategoriesWithBack(categories, backCatFlow.PendingType), cancellationToken: ct);
        return;
    }
    
    // Готово — редактируем сообщение на главное меню
    if (data == "txn:done" && _flow.TryGetValue(userId, out var doneFlow))
    {
        _flow.Remove(userId);
        await botClient.EditMessageTextAsync(chatId.Value, msgId!.Value, "Выберите действие:", 
            replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
        return;
    }
    
    // Отмена транзакции — удаляем и редактируем на главное меню
    if (data == "txn:cancel" && _flow.TryGetValue(userId, out var cancelFlow) && cancelFlow.PendingTransactionId.HasValue)
    {
        await transactionService.DeleteAsync(cancelFlow.PendingTransactionId.Value, ct);
        _flow.Remove(userId);
        await botClient.EditMessageTextAsync(chatId.Value, msgId!.Value, "❌ Транзакция отменена.\n\nВыберите действие:", 
            replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
        return;
    }


    // ЛИМИТЫ
    
    if (data == "limit:create")
    {
        await limitCmd.ShowCategoriesAsync(botClient, chatId.Value, userId, ct);
        _flow[userId] = new UserFlowState { Step = UserFlowStep.WaitingLimitCategory };
        return;
    }

    if (data == "limit:reset")
    {
        await limitService.ResetMonthlyLimitsAsync(userId, ct);
        await botClient.SendTextMessageAsync(chatId.Value, "✅ Месячные лимиты сброшены!", replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
        return;
    }

    if (data.StartsWith("limit:delete:") && int.TryParse(data.Split(':')[2], out var delLimitId))
    {
        await limitService.DeleteAsync(userId, delLimitId, ct);
        await limitCmd.ShowMenuAsync(botClient, chatId.Value, userId, ct);
        return;
    }

    if (data.StartsWith("limit:cat:") && int.TryParse(data.Split(':')[2], out var limitCatId))
    {
        _flow[userId] = new UserFlowState { Step = UserFlowStep.WaitingLimitAmount, PendingLimitCategoryId = limitCatId };
        await botClient.SendTextMessageAsync(chatId.Value, "Введите сумму лимита:", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
        return;
    }

    // === ЦЕЛИ ===
    
    if (data.StartsWith("goal:delete:") && int.TryParse(data.Split(':')[2], out var delGoalId))
    {
        await goalService.DeleteAsync(userId, delGoalId, ct);
        await botClient.SendTextMessageAsync(chatId.Value, "✅ Цель удалена", replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
        return;
    }

    if (data.StartsWith("goal:complete:") && int.TryParse(data.Split(':')[2], out var compGoalId))
    {
        await goalService.CompleteAsync(userId, compGoalId, ct);
        await botClient.SendTextMessageAsync(chatId.Value, "🎉 Цель завершена!", replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
        return;
    }

    if (data.StartsWith("goal:activate:") && int.TryParse(data.Split(':')[2], out var actGoalId))
    {
        await goalService.SetActiveAsync(userId, actGoalId, ct);
        await goalCmd.ShowMenuAsync(botClient, chatId.Value, userId, ct);
        return;
    }

    // === ДОЛГИ ===
    
    if (data.StartsWith("debt:pay:") && int.TryParse(data.Split(':')[2], out var payDebtId))
    {
        _flow[userId] = new UserFlowState { Step = UserFlowStep.WaitingDebtPayment, PendingDebtId = payDebtId };
        await botClient.SendTextMessageAsync(chatId.Value, "Введите сумму платежа:", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
        return;
    }

    if (data.StartsWith("debt:close:") && int.TryParse(data.Split(':')[2], out var closeDebtId))
    {
        await debtService.MarkAsPaidAsync(userId, closeDebtId, ct);
        await botClient.SendTextMessageAsync(chatId.Value, "✅ Долг закрыт!", replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
        return;
    }

    if (data.StartsWith("debt:delete:") && int.TryParse(data.Split(':')[2], out var delDebtId))
    {
        await debtService.DeleteAsync(userId, delDebtId, ct);
        await botClient.SendTextMessageAsync(chatId.Value, "✅ Долг удалён", replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
        return;
    }

    // === РЕГУЛЯРНЫЕ ПЛАТЕЖИ ===
    
    if (data.StartsWith("regular:pay:") && int.TryParse(data.Split(':')[2], out var payRegId))
    {
        var payment = await regularService.MarkAsPaidAsync(userId, payRegId, ct);
        if (payment != null)
            await botClient.SendTextMessageAsync(chatId.Value, $"✅ Платёж \"{payment.Name}\" оплачен!\nСледующий: {payment.NextDueDate:dd.MM.yyyy}", replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
        return;
    }

    if (data.StartsWith("regular:pause:") && int.TryParse(data.Split(':')[2], out var pauseId))
    {
        await regularService.SetPausedAsync(userId, pauseId, true, ct);
        await regularCmd.ShowMenuAsync(botClient, chatId.Value, userId, ct);
        return;
    }

    if (data.StartsWith("regular:resume:") && int.TryParse(data.Split(':')[2], out var resumeId))
    {
        await regularService.SetPausedAsync(userId, resumeId, false, ct);
        await regularCmd.ShowMenuAsync(botClient, chatId.Value, userId, ct);
        return;
    }

    if (data.StartsWith("regular:delete:") && int.TryParse(data.Split(':')[2], out var delRegId))
    {
        await regularService.DeleteAsync(userId, delRegId, ct);
        await botClient.SendTextMessageAsync(chatId.Value, "✅ Платёж удалён", replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
        return;
    }

    // === ОТМЕНА ПОСЛЕДНЕЙ ТРАНЗАКЦИИ ===
    
    if (data == "action:cancel_last_tx")
    {
        var lastTx = await transactionService.GetLastTransactionAsync(userId, ct);
        if (lastTx != null && !lastTx.IsError)
        {
            await transactionService.CancelAsync(lastTx.Id, ct);
            var sign = lastTx.Type == TransactionType.Income ? "+" : "-";
            await botClient.SendTextMessageAsync(chatId.Value, $"✅ Транзакция отменена\n{sign}{lastTx.Amount:F2} — {lastTx.Category?.Name}", replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
        }
        else
        {
            await botClient.SendTextMessageAsync(chatId.Value, "❌ Нет транзакций для отмены", replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
        }
        return;
    }
}

// ХЕЛПЕРЫ

Task SendMenuAsync(ITelegramBotClient botClient, long chatId, CancellationToken ct) =>
    botClient.SendTextMessageAsync(chatId, "Выберите действие:", replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);

// Генерация CSV для экспорта
async Task<string> GenerateCsvAsync(long userId, UserFlowState flow, ITransactionService txnService, CancellationToken ct)
{
    var date = flow.StatsDate;
    var from = flow.StatsPeriod switch
    {
        StatsPeriod.Week => date.AddDays(-(int)date.DayOfWeek + 1),
        StatsPeriod.Month => new DateTimeOffset(date.Year, date.Month, 1, 0, 0, 0, date.Offset),
        StatsPeriod.Year => new DateTimeOffset(date.Year, 1, 1, 0, 0, 0, date.Offset),
        _ => date.AddDays(-30)
    };
    var to = flow.StatsPeriod switch
    {
        StatsPeriod.Week => date.AddDays(7 - (int)date.DayOfWeek),
        StatsPeriod.Month => new DateTimeOffset(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month), 23, 59, 59, date.Offset),
        StatsPeriod.Year => new DateTimeOffset(date.Year, 12, 31, 23, 59, 59, date.Offset),
        _ => date
    };

    var transactions = await txnService.GetUserTransactionsAsync(userId, 1000, ct);
    var filtered = transactions.Where(t => t.Date >= from && t.Date <= to).ToList();

    var sb = new System.Text.StringBuilder();
    sb.AppendLine("Дата,Тип,Категория,Сумма,Описание,На эмоциях");
    foreach (var t in filtered)
    {
        var type = t.Type == TransactionType.Income ? "Доход" : "Расход";
        var cat = t.Category?.Name ?? "";
        var desc = t.Description?.Replace(",", " ") ?? "";
        var emo = t.IsImpulsive ? "Да" : "Нет";
        sb.AppendLine($"{t.Date:dd.MM.yyyy},{type},{cat},{t.Amount:F2},{desc},{emo}");
    }
    return sb.ToString();
}
