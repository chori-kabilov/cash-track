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
using Console.Handlers;

// Глобальное состояние диалогов пользователей
var _cts = new CancellationTokenSource();
Dictionary<long, UserFlowState> _flow = new();

// Защита от запуска нескольких экземпляров
using var mutex = new Mutex(true, "Global\\CashTrack.TelegramBot", out var isNewInstance);
if (!isNewInstance)
{
    System.Console.WriteLine("Бот уже запущен. Выход.");
    return;
}

// 1. Конфигурация
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddEnvironmentVariables()
    .Build();

// 2. База данных
var dbOptions = new DbContextOptionsBuilder<DataContext>()
    .UseNpgsql(config.GetConnectionString("DefaultConnection"))
    .Options;
var db = new DataContext(dbOptions);

// 3. Сервисы
var userService = new UserService(db);
var accountService = new AccountService(db);
var transactionService = new TransactionService(db);
var categoryService = new CategoryService(db);
var goalService = new GoalService(db);
var debtService = new DebtService(db);
var regularService = new RegularPaymentService(db);
var limitService = new LimitService(db);

// 4. Команды
var startCmd = new StartCommand(userService, categoryService);
var helpCmd = new HelpCommand();
var balanceCmd = new BalanceCommand(accountService, goalService, debtService, regularService, transactionService);
var statsCmd = new StatsCommand(transactionService, limitService, regularService);
var goalCmd = new GoalCommand(goalService, accountService, transactionService, categoryService);
var debtCmd = new DebtCommand(debtService, accountService, transactionService, categoryService);
var regularCmd = new RegularPaymentCommand(regularService, accountService, transactionService, categoryService);
var limitCmd = new LimitCommand(limitService, categoryService);

// 5. Обработчики диалогов (модульные)
var transactionFlowHandler = new TransactionFlowHandler(categoryService, transactionService, accountService, limitService);
var goalFlowHandler = new GoalFlowHandler(goalService, goalCmd);
var debtFlowHandler = new DebtFlowHandler(debtService, debtCmd);
var regularFlowHandler = new RegularFlowHandler(regularService, regularCmd);
var limitFlowHandler = new LimitFlowHandler(limitService, categoryService);

var flowRouter = new FlowRouter(new IFlowStepHandler[]
{
    transactionFlowHandler,
    goalFlowHandler,
    debtFlowHandler,
    regularFlowHandler,
    limitFlowHandler
});

// 6. Роутер callback-запросов
var callbackRouter = new CallbackRouter(new ICallbackHandler[]
{
    new MenuCallbackHandler(balanceCmd, statsCmd, goalCmd, debtCmd, regularCmd, limitCmd, helpCmd),
    new BalanceCallbackHandler(balanceCmd, transactionService),
    new StatCallbackHandler(statsCmd, transactionService),
    new GoalCallbackHandler(goalCmd, goalService),
    new TransactionCallbackHandler(transactionFlowHandler, transactionService),
    new DebtCallbackHandler(debtCmd, debtService),
    new RegularCallbackHandler(regularCmd, regularService, categoryService),
    new LimitCallbackHandler(limitCmd, limitService),
    new GlobalCallbackHandler(transactionFlowHandler, transactionService)
});

// 7. Telegram бот
var botToken = config["BotToken"] ?? throw new Exception("BotToken не найден в конфигурации!");
var bot = new TelegramBotClient(botToken);
var me = await bot.GetMeAsync();
System.Console.WriteLine($"Бот @{me.Username} запущен");

// 8. Фоновый планировщик (напоминания)
var scheduler = new Console.Services.SchedulerService(bot, dbOptions);
scheduler.Start();

// 9. Запуск polling
bot.StartReceiving(
    updateHandler: HandleUpdateAsync,
    pollingErrorHandler: (_, ex, _) => { System.Console.WriteLine(ex); return Task.CompletedTask; },
    receiverOptions: new ReceiverOptions { AllowedUpdates = [] },
    cancellationToken: _cts.Token);

System.Console.WriteLine("Нажмите Ctrl+C для остановки.");
try { await Task.Delay(Timeout.InfiniteTimeSpan, _cts.Token); }
catch (OperationCanceledException) { }

// === ОБРАБОТЧИКИ ===

async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken ct)
{
    try
    {
        // Callback-запросы (нажатия кнопок)
        if (update.CallbackQuery is { } cb)
        {
            await HandleCallbackAsync(botClient, cb, ct);
            return;
        }

        // Текстовые сообщения
        if (update.Message is not { Text: { } text } msg || msg.From is null) return;
        
        var chatId = msg.Chat.Id;
        var userId = msg.From.Id;
        text = text.Trim();
        
        if (string.IsNullOrEmpty(text)) return;
        System.Console.WriteLine($"[{userId}] {text}");

        // Команды
        if (text.StartsWith('/'))
        {
            await HandleCommandAsync(botClient, chatId, userId, text, msg.From, ct);
            return;
        }

        // Диалоговый поток
        if (_flow.TryGetValue(userId, out var flow))
        {
            await flowRouter.HandleAsync(botClient, chatId, userId, text, flow, _flow, ct);
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

async Task HandleCommandAsync(ITelegramBotClient botClient, long chatId, long userId, string text, User from, CancellationToken ct)
{
    if (text.Equals("/start", StringComparison.OrdinalIgnoreCase))
    {
        await startCmd.ExecuteAsync(botClient, chatId, from, ct);
        return;
    }

    // Оплата долга: /pay_debt_123
    if (text.StartsWith("/pay_debt_") && int.TryParse(text[10..], out var debtId))
    {
        _flow[userId] = new UserFlowState { Step = UserFlowStep.WaitingDebtPayment, PendingDebtId = debtId };
        await botClient.SendTextMessageAsync(chatId, "Введите сумму платежа:", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
        return;
    }

    // Оплата регулярного платежа: /pay_regular_123
    if (text.StartsWith("/pay_regular_") && int.TryParse(text[13..], out var regId))
    {
        var payment = await regularService.GetByIdAsync(userId, regId, ct);
        if (payment == null)
        {
            await botClient.SendTextMessageAsync(chatId, "❌ Платеж не найден.", replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
            return;
        }

        await regularService.MarkAsPaidAsync(userId, regId, null, ct);
        var catId = payment.CategoryId ?? (await categoryService.GetUserCategoriesAsync(userId, ct)).FirstOrDefault(c => c.Type == TransactionType.Expense)?.Id;
        
        if (catId.HasValue)
            await transactionFlowHandler.AddTransactionWithDescriptionAsync(botClient, chatId, userId, payment.Amount, catId.Value, TransactionType.Expense, $"Регулярный: {payment.Name}", false, ct);

        await botClient.SendTextMessageAsync(chatId, $"✅ \"{payment.Name}\" оплачен! След: {payment.NextDueDate:dd.MM}", replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
        return;
    }

    await SendMenuAsync(botClient, chatId, ct);
}

async Task HandleCallbackAsync(ITelegramBotClient botClient, CallbackQuery cb, CancellationToken ct)
{
    var chatId = cb.Message?.Chat.Id;
    if (chatId == null) return;

    var userId = cb.From.Id;
    
    // Подтверждаем callback (убирает часики)
    try { await botClient.AnswerCallbackQueryAsync(cb.Id, cancellationToken: ct); }
    catch (ApiRequestException) { }

    // Роутинг по handlers
    _flow.TryGetValue(userId, out var flow);
    await callbackRouter.RouteAsync(botClient, cb, flow, _flow, ct);
}

Task SendMenuAsync(ITelegramBotClient botClient, long chatId, CancellationToken ct) =>
    botClient.SendTextMessageAsync(chatId, "Выберите действие:", replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
