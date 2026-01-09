using System.Globalization;
using Console.Bot;
using Domain.Enums;
using Infrastructure.Services;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace Console.Flow;

// Обработчик многшаговых диалогов с пользователем (цели, долги, транзакции и т.д.)
public class FlowHandler(
    ICategoryService categoryService,
    IGoalService goalService,
    IDebtService debtService,
    IRegularPaymentService regularPaymentService,
    ITransactionService transactionService,
    IAccountService accountService,
    ILimitService limitService)
{
    // Обработка текстового ввода пользователя в зависимости от текущего шага диалога
    public async Task<bool> HandleAsync(
        ITelegramBotClient bot, 
        long chatId, 
        long userId, 
        string text, 
        UserFlowState flow, 
        Dictionary<long, UserFlowState> flowDict,
        CancellationToken ct)
    {
        switch (flow.Step)
        {
            // === ТРАНЗАКЦИИ ===
            case UserFlowStep.WaitingAmount:
                return await HandleAmountAsync(bot, chatId, userId, text, flow, ct);
                
            case UserFlowStep.ChoosingCategory:
                return await HandleNewCategoryAsync(bot, chatId, userId, text, flow, ct);
                
            case UserFlowStep.WaitingDescription:
                await AddTransactionAsync(bot, chatId, userId, flow.PendingAmount, flow.PendingCategoryId!.Value, flow.PendingType, text, flow.PendingIsImpulsive, ct);
                flowDict.Remove(userId);
                return true;

            // === ЦЕЛИ ===
            case UserFlowStep.WaitingGoalName:
                flow.PendingGoalName = text;
                flow.Step = UserFlowStep.WaitingGoalTarget;
                await bot.SendTextMessageAsync(chatId, "Введите сумму цели:", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
                return true;

            case UserFlowStep.WaitingGoalTarget:
                if (!TryParseAmount(text, out var goalAmount) || goalAmount <= 0)
                {
                    await bot.SendTextMessageAsync(chatId, "❌ Неверная сумма.", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
                    return true;
                }
                flow.PendingGoalTarget = goalAmount;
                flow.Step = UserFlowStep.WaitingGoalDeadline;
                await bot.SendTextMessageAsync(chatId, "Введите дедлайн (ДД.ММ.ГГГГ) или 'нет':", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
                return true;

            case UserFlowStep.WaitingGoalDeadline:
                return await HandleGoalDeadlineAsync(bot, chatId, userId, text, flow, flowDict, ct);

            case UserFlowStep.WaitingGoalDeposit:
                return await HandleGoalDepositAsync(bot, chatId, userId, text, flow, flowDict, ct);

            // === ДОЛГИ ===
            case UserFlowStep.WaitingDebtName:
                flow.PendingDebtName = text;
                flow.Step = UserFlowStep.WaitingDebtAmount;
                await bot.SendTextMessageAsync(chatId, "Введите сумму долга:", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
                return true;

            case UserFlowStep.WaitingDebtAmount:
                if (!TryParseAmount(text, out var debtAmount) || debtAmount <= 0)
                {
                    await bot.SendTextMessageAsync(chatId, "❌ Неверная сумма.", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
                    return true;
                }
                flow.PendingDebtAmount = debtAmount;
                flow.Step = UserFlowStep.WaitingDebtDeadline;
                await bot.SendTextMessageAsync(chatId, "Срок возврата (ДД.ММ.ГГГГ) или 'нет':", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
                return true;

            case UserFlowStep.WaitingDebtDeadline:
                return await HandleDebtDeadlineAsync(bot, chatId, userId, text, flow, flowDict, ct);

            case UserFlowStep.WaitingDebtPayment:
                return await HandleDebtPaymentAsync(bot, chatId, userId, text, flow, flowDict, ct);

            // === РЕГУЛЯРНЫЕ ПЛАТЕЖИ ===
            case UserFlowStep.WaitingRegularName:
                flow.PendingRegularName = text;
                flow.Step = UserFlowStep.WaitingRegularAmount;
                await bot.SendTextMessageAsync(chatId, "Введите сумму:", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
                return true;

            case UserFlowStep.WaitingRegularAmount:
                if (!TryParseAmount(text, out var regAmount) || regAmount <= 0)
                {
                    await bot.SendTextMessageAsync(chatId, "❌ Неверная сумма.", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
                    return true;
                }
                flow.PendingRegularAmount = regAmount;
                flow.Step = UserFlowStep.WaitingRegularFrequency;
                await bot.SendTextMessageAsync(chatId, "Как часто?", replyMarkup: FrequencyKeyboard(), cancellationToken: ct);
                return true;

            case UserFlowStep.WaitingRegularDate:
                return await HandleRegularDateAsync(bot, chatId, userId, text, flow, flowDict, ct);

            // === ЛИМИТЫ ===
            case UserFlowStep.WaitingLimitAmount:
                return await HandleLimitAmountAsync(bot, chatId, userId, text, flow, flowDict, ct);

            // === НОВАЯ КАТЕГОРИЯ ===
            case UserFlowStep.WaitingNewCategory:
                return await HandleNewCategoryAsync(bot, chatId, userId, text, flow, ct);

            default:
                return false;
        }
    }

    // === ПРИВАТНЫЕ МЕТОДЫ ===

    private async Task<bool> HandleAmountAsync(ITelegramBotClient bot, long chatId, long userId, string text, UserFlowState flow, CancellationToken ct)
    {
        // Парсинг суммы и опционального описания: "5000 премия за проект"
        var parts = text.Trim().Split(' ', 2);
        if (!TryParseAmount(parts[0], out var amount) || amount <= 0)
        {
            await bot.SendTextMessageAsync(chatId, "❌ Неверная сумма.", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
            return true;
        }

        flow.PendingAmount = amount;
        flow.PendingDescription = parts.Length > 1 ? parts[1].Trim() : null;
        flow.Step = UserFlowStep.ChoosingCategory;

        var categories = await GetSuggestedCategoriesAsync(userId, flow.PendingType, ct);
        
        var prompt = flow.PendingType == TransactionType.Income ? "Откуда доход?" : "Выберите категорию:";
        
        // После ввода текста — ОТПРАВЛЯЕМ НОВОЕ сообщение (Принцип Диалоговой Последовательности)
        var catMsg = await bot.SendTextMessageAsync(chatId, prompt, 
            replyMarkup: BotInlineKeyboards.CategoriesWithBack(categories, flow.PendingType), cancellationToken: ct);
        flow.PendingMessageId = catMsg.MessageId;
        return true;
    }

    private async Task<bool> HandleNewCategoryAsync(ITelegramBotClient bot, long chatId, long userId, string text, UserFlowState flow, CancellationToken ct)
    {
        var name = text.Length > 20 ? text[..20] : text;
        var newCat = await categoryService.CreateAsync(userId, name, flow.PendingType, "🆕", ct);

        flow.PendingCategoryId = newCat.Id;
        
        // После ввода текста — ОТПРАВЛЯЕМ НОВОЕ сообщение (Принцип Диалоговой Последовательности)
        // Очищаем PendingMessageId, чтобы AddTransactionAsync отправил новое сообщение
        flow.PendingMessageId = null;
        
        var (txnId, msgId) = await AddTransactionAsync(bot, chatId, userId, flow, ct);
        if (txnId.HasValue)
        {
            flow.PendingTransactionId = txnId;
            flow.PendingMessageId = msgId;
            flow.Step = UserFlowStep.None;
        }
        return true;
    }

    private async Task<bool> HandleGoalDeadlineAsync(ITelegramBotClient bot, long chatId, long userId, string text, UserFlowState flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        DateTimeOffset? deadline = null;
        if (DateTime.TryParseExact(text, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            deadline = new DateTimeOffset(d, TimeSpan.Zero);
        else if (!text.Contains("нет", StringComparison.OrdinalIgnoreCase))
        {
            await bot.SendTextMessageAsync(chatId, "❌ Неверный формат.", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
            return true;
        }

        await goalService.CreateAsync(userId, flow.PendingGoalName!, flow.PendingGoalTarget, deadline, ct);
        flowDict.Remove(userId);
        await bot.SendTextMessageAsync(chatId, "✅ Цель создана!", replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
        return true;
    }

    private async Task<bool> HandleGoalDepositAsync(ITelegramBotClient bot, long chatId, long userId, string text, UserFlowState flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        if (!TryParseAmount(text, out var amount) || amount <= 0)
        {
            await bot.SendTextMessageAsync(chatId, "❌ Неверная сумма.", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
            return true;
        }

        var cats = await categoryService.GetUserCategoriesAsync(userId, ct);
        var savings = cats.FirstOrDefault(c => c.Name == "Накопления" && c.Type == TransactionType.Expense) 
                      ?? cats.FirstOrDefault(c => c.Type == TransactionType.Expense);
        
        if (savings != null)
            await AddTransactionAsync(bot, chatId, userId, amount, savings.Id, TransactionType.Expense, "Пополнение цели", false, ct);

        await goalService.AddFundsAsync(userId, flow.PendingGoalId!.Value, amount, ct);
        flowDict.Remove(userId);

        var goal = (await goalService.GetUserGoalsAsync(userId, ct)).FirstOrDefault(g => g.Id == flow.PendingGoalId);
        var msg = $"✅ Пополнено на {amount:F2}!";
        if (goal?.IsCompleted == true) msg += $"\n🎉 Цель \"{goal.Name}\" достигнута!";
        
        await bot.SendTextMessageAsync(chatId, msg, replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
        return true;
    }

    private async Task<bool> HandleDebtDeadlineAsync(ITelegramBotClient bot, long chatId, long userId, string text, UserFlowState flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        DateTimeOffset? deadline = null;
        if (DateTime.TryParseExact(text, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            deadline = new DateTimeOffset(d, TimeSpan.Zero);

        await debtService.CreateAsync(userId, flow.PendingDebtName!, flow.PendingDebtAmount, flow.PendingDebtType, null, deadline, ct);
        flowDict.Remove(userId);
        await bot.SendTextMessageAsync(chatId, "✅ Долг записан!", replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
        return true;
    }

    private async Task<bool> HandleDebtPaymentAsync(ITelegramBotClient bot, long chatId, long userId, string text, UserFlowState flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        if (!TryParseAmount(text, out var amount) || amount <= 0)
        {
            await bot.SendTextMessageAsync(chatId, "❌ Неверная сумма.", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
            return true;
        }

        await debtService.MakePaymentAsync(userId, flow.PendingDebtId!.Value, amount, ct);

        var debt = await debtService.GetByIdAsync(userId, flow.PendingDebtId!.Value, ct);
        if (debt != null)
        {
            var cats = await categoryService.GetUserCategoriesAsync(userId, ct);
            var type = debt.Type == DebtType.IOwe ? TransactionType.Expense : TransactionType.Income;
            var cat = cats.FirstOrDefault(x => x.Name == "Долги") ?? cats.FirstOrDefault(x => x.Type == type);
            if (cat != null)
                await AddTransactionAsync(bot, chatId, userId, amount, cat.Id, type, $"Возврат: {debt.PersonName}", false, ct);
        }

        flowDict.Remove(userId);
        await bot.SendTextMessageAsync(chatId, "✅ Платёж учтён!", replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
        return true;
    }

    private async Task<bool> HandleRegularDateAsync(ITelegramBotClient bot, long chatId, long userId, string text, UserFlowState flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        if (!DateTime.TryParseExact(text, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
        {
            await bot.SendTextMessageAsync(chatId, "❌ Неверная дата.", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
            return true;
        }

        await regularPaymentService.CreateAsync(userId, flow.PendingRegularName!, flow.PendingRegularAmount, 
            flow.PendingRegularFrequency, null, null, 3, new DateTimeOffset(d, TimeSpan.Zero), ct);
        flowDict.Remove(userId);
        await bot.SendTextMessageAsync(chatId, "✅ Платеж создан!", replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
        return true;
    }

    // === ХЕЛПЕРЫ ===

    public async Task AddTransactionAsync(ITelegramBotClient bot, long chatId, long userId, decimal amount, int categoryId, TransactionType type, string? description, bool isImpulsive, CancellationToken ct)
    {
        try
        {
            // Проверка блокировки категории для расходов
            if (type == TransactionType.Expense)
            {
                var isBlocked = await limitService.IsCategoryBlockedAsync(userId, categoryId, ct);
                if (isBlocked)
                {
                    var category = await categoryService.GetCategoryByIdAsync(userId, categoryId, ct);
                    var catName = category != null ? $"{category.Icon} {category.Name}" : "категория";
                    await bot.SendTextMessageAsync(chatId, 
                        $"🔒 *Категория заблокирована!*\n\n{catName}\n\n_Лимит превышен. Расходы временно заблокированы._", 
                        Telegram.Bot.Types.Enums.ParseMode.Markdown, replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
                    return;
                }
            }

            await transactionService.ProcessTransactionAsync(userId, categoryId, amount, type, description, isImpulsive, null, ct);
            var account = await accountService.GetUserAccountAsync(userId, ct);
            var cat = await categoryService.GetCategoryByIdAsync(userId, categoryId, ct);

            var sign = type == TransactionType.Income ? "+" : "-";
            var emoji = type == TransactionType.Income ? "✅" : "🛍️";
            var catName2 = cat != null ? $"{cat.Name} {cat.Icon}" : "";
            var desc = !string.IsNullOrEmpty(description) ? $"\n📝 *{description}*" : "";
            var imp = isImpulsive ? "\n⚡ На эмоциях" : "";
            
            // Обновляем лимит для расходов
            var limitWarning = "";
            if (type == TransactionType.Expense)
            {
                var (limit, warningLevel) = await limitService.AddSpendingAsync(userId, categoryId, amount, ct);
                if (limit != null && warningLevel > 0)
                {
                    var percent = limit.Amount > 0 ? (limit.SpentAmount / limit.Amount) * 100 : 0;
                    limitWarning = warningLevel switch
                    {
                        100 => $"\n\n🔴 *Лимит превышен!* ({percent:F0}%)\n_Категория заблокирована на 24 часа_",
                        80 => $"\n\n⚠️ *Внимание!* Лимит на {percent:F0}%",
                        50 => $"\n\n📊 Лимит на {percent:F0}%",
                        _ => ""
                    };
                }
            }

            await bot.SendTextMessageAsync(chatId,
                $"{emoji} *{sign}{amount:F2} {account?.Currency}*\n📂 *{catName2}*{desc}{imp}{limitWarning}\n\n💰 Баланс: *{account?.Balance:F2}*",
                Telegram.Bot.Types.Enums.ParseMode.Markdown, replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
        }
        catch (Exception ex)
        {
            System.Console.WriteLine(ex);
            await bot.SendTextMessageAsync(chatId, "❌ Ошибка: " + ex.Message, replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
        }
    }

    // Унифицированный метод для записи транзакции из UserFlowState
    // Возвращает (transactionId, messageId) для возможности отмены
    public async Task<(int? TxnId, int? MsgId)> AddTransactionAsync(ITelegramBotClient bot, long chatId, long userId, UserFlowState flow, CancellationToken ct)
    {
        try
        {
            var type = flow.PendingType;
            var amount = flow.PendingAmount;
            var categoryId = flow.PendingCategoryId!.Value;
            var description = flow.PendingDescription;
            var isImpulsive = flow.PendingIsImpulsive;

            // Проверка блокировки категории для расходов
            if (type == TransactionType.Expense)
            {
                var isBlocked = await limitService.IsCategoryBlockedAsync(userId, categoryId, ct);
                if (isBlocked)
                {
                    var category = await categoryService.GetCategoryByIdAsync(userId, categoryId, ct);
                    var catName = category != null ? $"{category.Icon} {category.Name}" : "категория";
                    await bot.SendTextMessageAsync(chatId, 
                        $"🔒 *Категория заблокирована!*\n\n{catName}\n\n_Лимит превышен. Расходы временно заблокированы._", 
                        Telegram.Bot.Types.Enums.ParseMode.Markdown, replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
                    return (null, null);
                }
            }

            var txn = await transactionService.ProcessTransactionAsync(userId, categoryId, amount, type, description, isImpulsive, null, ct);
            var account = await accountService.GetUserAccountAsync(userId, ct);
            var cat = await categoryService.GetCategoryByIdAsync(userId, categoryId, ct);

            var sign = type == TransactionType.Income ? "+" : "-";
            var emoji = type == TransactionType.Income ? "✅" : "🛍️";
            var typeName = type == TransactionType.Income ? "Доход записан!" : "Расход записан!";
            var catName2 = cat != null ? $"{cat.Icon} {cat.Name}" : "";
            var descText = !string.IsNullOrEmpty(description) ? $"\n📝 {description}" : "";
            var impText = isImpulsive ? "\n🌪 На эмоциях" : "";
            
            // Обновляем лимит для расходов
            var limitWarning = "";
            if (type == TransactionType.Expense)
            {
                var (limit, warningLevel) = await limitService.AddSpendingAsync(userId, categoryId, amount, ct);
                if (limit != null && warningLevel > 0)
                {
                    var percent = limit.Amount > 0 ? (limit.SpentAmount / limit.Amount) * 100 : 0;
                    limitWarning = warningLevel switch
                    {
                        100 => $"\n\n🔴 *Лимит превышен!* ({percent:F0}%)\n_Категория заблокирована на 24 часа_",
                        80 => $"\n\n⚠️ *Внимание!* Лимит на {percent:F0}%",
                        50 => $"\n\n📊 Лимит на {percent:F0}%",
                        _ => ""
                    };
                }
            }
            
            var balanceText = account?.Balance.ToString("F0") ?? "0";

            // После нажатия кнопки — РЕДАКТИРУЕМ сообщение (Принцип Диалоговой Последовательности)
            var resultText = $"{emoji} *{typeName}*\n\n{sign}{amount:F0} TJS\n📂 {catName2}{descText}{impText}{limitWarning}\n\n💰 Баланс: ||{balanceText} TJS||";
            
            if (flow.PendingMessageId.HasValue)
            {
                await bot.EditMessageTextAsync(chatId, flow.PendingMessageId.Value, resultText,
                    Telegram.Bot.Types.Enums.ParseMode.Markdown, 
                    replyMarkup: BotInlineKeyboards.TransactionComplete(), 
                    cancellationToken: ct);
                return (txn.Id, flow.PendingMessageId);
            }
            else
            {
                var msg = await bot.SendTextMessageAsync(chatId, resultText,
                    Telegram.Bot.Types.Enums.ParseMode.Markdown, 
                    replyMarkup: BotInlineKeyboards.TransactionComplete(), 
                    cancellationToken: ct);
                return (txn.Id, msg.MessageId);
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine(ex);
            await bot.SendTextMessageAsync(chatId, "❌ Ошибка: " + ex.Message, replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
            return (null, null);
        }
    }

    // Escape для MarkdownV2
    private static string EscapeMd(string text) => 
        text.Replace("_", "\\_").Replace("*", "\\*").Replace("[", "\\[").Replace("]", "\\]")
            .Replace("(", "\\(").Replace(")", "\\)").Replace("~", "\\~").Replace("`", "\\`")
            .Replace(">", "\\>").Replace("#", "\\#").Replace("+", "\\+").Replace("-", "\\-")
            .Replace("=", "\\=").Replace("|", "\\|").Replace("{", "\\{").Replace("}", "\\}")
            .Replace(".", "\\.").Replace("!", "\\!");

    public async Task<IReadOnlyList<Domain.Entities.Category>> GetSuggestedCategoriesAsync(long userId, TransactionType type, CancellationToken ct)
    {
        var recentIds = await transactionService.GetRecentCategoryIdsAsync(userId, type, 6, ct);
        var all = await categoryService.GetUserCategoriesAsync(userId, ct);
        
        if (!all.Any())
        {
            await categoryService.InitializeDefaultCategoriesAsync(userId, ct);
            all = await categoryService.GetUserCategoriesAsync(userId, ct);
        }

        var relevant = all.Where(c => c.Type == type).ToList();
        var result = new List<Domain.Entities.Category>();
        
        foreach (var id in recentIds)
        {
            var c = relevant.FirstOrDefault(x => x.Id == id);
            if (c != null) result.Add(c);
        }
        
        foreach (var c in relevant.OrderBy(x => x.Priority))
        {
            if (!result.Contains(c)) result.Add(c);
            if (result.Count >= 9) break;
        }
        
        return result;
    }

    private static bool TryParseAmount(string text, out decimal amount) =>
        decimal.TryParse(text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out amount);

    private static InlineKeyboardMarkup FrequencyKeyboard() => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("Ежедневно", "reg:freq:Daily"), InlineKeyboardButton.WithCallbackData("Еженедельно", "reg:freq:Weekly") },
        new[] { InlineKeyboardButton.WithCallbackData("Ежемесячно", "reg:freq:Monthly"), InlineKeyboardButton.WithCallbackData("Ежегодно", "reg:freq:Yearly") }
    });

    // Обработка ввода суммы лимита
    private async Task<bool> HandleLimitAmountAsync(ITelegramBotClient bot, long chatId, long userId, string text, UserFlowState flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        if (!TryParseAmount(text, out var amount) || amount <= 0)
        {
            await bot.SendTextMessageAsync(chatId, "❌ Неверная сумма.", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
            return true;
        }

        if (!flow.PendingLimitCategoryId.HasValue)
        {
            await bot.SendTextMessageAsync(chatId, "❌ Категория не выбрана.", replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
            flowDict.Remove(userId);
            return true;
        }

        await limitService.CreateAsync(userId, flow.PendingLimitCategoryId.Value, amount, ct);
        
        var category = await categoryService.GetCategoryByIdAsync(userId, flow.PendingLimitCategoryId.Value, ct);
        var catName = category != null ? $"{category.Icon} {category.Name}" : "категория";
        
        flowDict.Remove(userId);
        await bot.SendTextMessageAsync(chatId, $"✅ Лимит создан!\n\n{catName}: {amount:F0} / месяц", replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
        return true;
    }
}
