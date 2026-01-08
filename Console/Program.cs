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
var limitService = new LimitService(db);
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
            // Для дохода — добавляем сообщение пользователя в список на удаление
            if (flow.PendingType == TransactionType.Income)
            {
                flow.MessageIdsToDelete.Add(msg.MessageId);
            }
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

    // Добавить описание к доходу (опционально)
    if (data == "action:add_income_desc" && _flow.TryGetValue(userId, out var incFlow) && incFlow.Step == UserFlowStep.WaitingIncomeDescription)
    {
        await botClient.SendTextMessageAsync(chatId.Value, "Введите описание:", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
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
            case "menu:limits": await limitCmd.ShowMenuAsync(botClient, chatId.Value, userId, ct, msgId); return;
            case "menu:income":
                _flow[userId] = new UserFlowState { Step = UserFlowStep.WaitingAmount, PendingType = TransactionType.Income, PendingMessageId = msgId };
                await botClient.EditMessageTextAsync(chatId.Value, msgId!.Value, 
                    "💵 *Доход*\n\nВведите сумму:\n_Можно добавить описание через пробел_\n_Пример: 5000 премия_", 
                    ParseMode.Markdown, replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
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
    
    if (data == "cat:new" && _flow.TryGetValue(userId, out var newCatFlow))
    {
        // Добавляем сообщение с категориями в список на удаление
        if (newCatFlow.PendingType == TransactionType.Income && newCatFlow.PendingMessageId.HasValue)
        {
            newCatFlow.MessageIdsToDelete.Add(newCatFlow.PendingMessageId.Value);
        }
        
        newCatFlow.Step = UserFlowStep.ChoosingCategory; // Ожидаем название категории
        var newMsg = await botClient.SendTextMessageAsync(chatId.Value, "✏️ Напишите название категории:", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
        newCatFlow.PendingMessageId = newMsg.MessageId;
        return;
    }

    if (data.StartsWith("cat:"))
    {
        var parts = data.Split(':');
        if (parts.Length == 3 && int.TryParse(parts[2], out var catId) && _flow.TryGetValue(userId, out var catFlow) && catFlow.Step == UserFlowStep.ChoosingCategory)
        {
            catFlow.PendingCategoryId = catId;
            
            // Для дохода — добавить сообщение с категориями в список, записать и показать результат
            if (catFlow.PendingType == TransactionType.Income)
            {
                // Добавляем сообщение с категориями в список на удаление
                if (catFlow.PendingMessageId.HasValue)
                {
                    catFlow.MessageIdsToDelete.Add(catFlow.PendingMessageId.Value);
                }
                
                var (txnId, incomeMsgId) = await flowHandler.AddIncomeAsync(botClient, chatId.Value, userId, catFlow.PendingAmount, catId, catFlow.PendingDescription, ct);
                if (txnId.HasValue)
                {
                    catFlow.PendingTransactionId = txnId;
                    catFlow.PendingMessageId = incomeMsgId;
                    catFlow.Step = UserFlowStep.WaitingIncomeDescription;
                }
                else
                {
                    _flow.Remove(userId);
                }
                return;
            }
            
            // Для расхода — старый flow с описанием и "на эмоциях"
            catFlow.Step = UserFlowStep.WaitingDescription;
            catFlow.PendingIsImpulsive = false;
            await botClient.SendTextMessageAsync(chatId.Value, "Введите описание (или пропустить):", replyMarkup: BotInlineKeyboards.SkipDescription(false), cancellationToken: ct);
            return;
        }
        await SendMenuAsync(botClient, chatId.Value, ct);
    }

    // === ДОХОД ===
    
    // Готово — удалить все сообщения по порядку и показать меню
    if (data == "income:done" && _flow.TryGetValue(userId, out var doneFlow))
    {
        // Добавляем последнее сообщение (результат) в конец списка
        if (doneFlow.PendingMessageId.HasValue)
        {
            doneFlow.MessageIdsToDelete.Add(doneFlow.PendingMessageId.Value);
        }
        
        // Удаляем все сообщения в фоне по порядку (первое отправленное = первое удалённое)
        var messagesToDelete = doneFlow.MessageIdsToDelete.ToList();
        var chatIdCopy = chatId.Value;
        _ = Task.Run(async () =>
        {
            await Task.Delay(10000); // Начальная задержка
            for (int i = 0; i < messagesToDelete.Count; i++)
            {
                try { await botClient.DeleteMessageAsync(chatIdCopy, messagesToDelete[i]); } catch { }
                if (i < messagesToDelete.Count - 1) await Task.Delay(1000); // 1 сек между удалениями
            }
        });
        
        _flow.Remove(userId);
        await SendMenuAsync(botClient, chatId.Value, ct);
        return;
    }

    // Добавить описание — изменить сообщение на ввод
    if (data == "income:add_desc" && _flow.TryGetValue(userId, out var descFlow) && descFlow.Step == UserFlowStep.WaitingIncomeDescription)
    {
        if (descFlow.PendingMessageId.HasValue)
        {
            await botClient.EditMessageTextAsync(chatId.Value, descFlow.PendingMessageId.Value, 
                "📝 Введите описание:", replyMarkup: BotInlineKeyboards.IncomeDescription(), cancellationToken: ct);
        }
        return;
    }

    // Назад — вернуть результат
    if (data == "income:back" && _flow.TryGetValue(userId, out var backFlow) && backFlow.Step == UserFlowStep.WaitingIncomeDescription)
    {
        if (backFlow.PendingMessageId.HasValue && backFlow.PendingTransactionId.HasValue)
        {
            var txn = await transactionService.GetByIdAsync(backFlow.PendingTransactionId.Value, ct);
            var account = await accountService.GetUserAccountAsync(userId, ct);
            if (txn != null)
            {
                var cat = txn.Category;
                var catName = cat != null ? $"{cat.Icon} {cat.Name}" : "";
                var descText = !string.IsNullOrEmpty(txn.Description) ? $"\n📝 {txn.Description}" : "";
                var balanceText = account?.Balance.ToString("F0") ?? "0";
                
                await botClient.EditMessageTextAsync(chatId.Value, backFlow.PendingMessageId.Value,
                    $"✅ *Доход записан\\!*\n\n\\+{txn.Amount:F0} TJS\n📂 {EscapeMd(catName)}{EscapeMd(descText)}\n\n💰 Баланс: ||{balanceText} TJS||",
                    ParseMode.MarkdownV2, replyMarkup: BotInlineKeyboards.IncomeComplete(!string.IsNullOrEmpty(txn.Description)), cancellationToken: ct);
            }
        }
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

// Escape для MarkdownV2
string EscapeMd(string text) => 
    text.Replace("_", "\\_").Replace("*", "\\*").Replace("[", "\\[").Replace("]", "\\]")
        .Replace("(", "\\(").Replace(")", "\\)").Replace("~", "\\~").Replace("`", "\\`")
        .Replace(">", "\\>").Replace("#", "\\#").Replace("+", "\\+").Replace("-", "\\-")
        .Replace("=", "\\=").Replace("|", "\\|").Replace("{", "\\{").Replace("}", "\\}")
        .Replace(".", "\\.").Replace("!", "\\!");
