using Console.Bot;
using Domain.Enums;
using Infrastructure.Services;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace Console.Flow;

// Обработчик шагов транзакций (доходы/расходы)
public class TransactionFlowHandler(
    ICategoryService categoryService,
    ITransactionService transactionService,
    IAccountService accountService,
    ILimitService limitService) : IFlowStepHandler
{
    private static readonly UserFlowStep[] HandledSteps = 
    {
        UserFlowStep.WaitingAmount,
        UserFlowStep.ChoosingCategory,
        UserFlowStep.WaitingDescription,
        UserFlowStep.WaitingNewCategory
    };

    public bool CanHandle(UserFlowStep step) => HandledSteps.Contains(step);

    public async Task<bool> HandleAsync(ITelegramBotClient bot, long chatId, long userId, string text, 
        UserFlowState flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        return flow.Step switch
        {
            UserFlowStep.WaitingAmount => await HandleAmountAsync(bot, chatId, userId, text, flow, ct),
            UserFlowStep.ChoosingCategory or UserFlowStep.WaitingNewCategory => await HandleNewCategoryAsync(bot, chatId, userId, text, flow, ct),
            UserFlowStep.WaitingDescription => await HandleDescriptionAsync(bot, chatId, userId, text, flow, flowDict, ct),
            _ => false
        };
    }

    // Обработка ввода суммы (и опционального описания)
    private async Task<bool> HandleAmountAsync(ITelegramBotClient bot, long chatId, long userId, string text, UserFlowState flow, CancellationToken ct)
    {
        var parts = text.Trim().Split(' ', 2);
        if (!FlowHelper.TryParseAmount(parts[0], out var amount) || amount <= 0)
        {
            await bot.SendTextMessageAsync(chatId, "❌ Неверная сумма.", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
            return true;
        }

        flow.PendingAmount = amount;
        flow.PendingDescription = parts.Length > 1 ? parts[1].Trim() : null;
        flow.Step = UserFlowStep.ChoosingCategory;

        var categories = await GetSuggestedCategoriesAsync(userId, flow.PendingType, ct);
        var prompt = flow.PendingType == TransactionType.Income ? "Откуда доход?" : "Выберите категорию:";
        
        var catMsg = await bot.SendTextMessageAsync(chatId, prompt, 
            replyMarkup: BotInlineKeyboards.CategoriesWithBack(categories, flow.PendingType), cancellationToken: ct);
        flow.PendingMessageId = catMsg.MessageId;
        return true;
    }

    // Обработка ввода новой категории
    private async Task<bool> HandleNewCategoryAsync(ITelegramBotClient bot, long chatId, long userId, string text, UserFlowState flow, CancellationToken ct)
    {
        var name = text.Length > 20 ? text[..20] : text;
        var newCat = await categoryService.CreateAsync(userId, name, flow.PendingType, "🆕", ct);

        flow.PendingCategoryId = newCat.Id;
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

    // Обработка ввода описания
    private async Task<bool> HandleDescriptionAsync(ITelegramBotClient bot, long chatId, long userId, string text, UserFlowState flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        await AddTransactionWithDescriptionAsync(bot, chatId, userId, flow.PendingAmount, flow.PendingCategoryId!.Value, flow.PendingType, text, flow.PendingIsImpulsive, ct);
        flowDict.Remove(userId);
        return true;
    }

    // Запись транзакции из UserFlowState (возвращает ID для отмены)
    public async Task<(int? TxnId, int? MsgId)> AddTransactionAsync(ITelegramBotClient bot, long chatId, long userId, UserFlowState flow, CancellationToken ct)
    {
        try
        {
            var type = flow.PendingType;
            var amount = flow.PendingAmount;
            var categoryId = flow.PendingCategoryId!.Value;
            var description = flow.PendingDescription;
            var isImpulsive = flow.PendingIsImpulsive;

            // Проверка блокировки категории
            if (type == TransactionType.Expense)
            {
                var isBlocked = await limitService.IsCategoryBlockedAsync(userId, categoryId, ct);
                if (isBlocked)
                {
                    var category = await categoryService.GetCategoryByIdAsync(userId, categoryId, ct);
                    var catName = category != null ? $"{category.Icon} {category.Name}" : "категория";
                    await bot.SendTextMessageAsync(chatId, 
                        $"🔒 *Категория заблокирована!*\n\n{catName}\n\n_Лимит превышен._", 
                        ParseMode.Markdown, replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
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
            
            var limitWarning = await GetLimitWarningAsync(userId, categoryId, amount, type, ct);
            var balanceText = account?.Balance.ToString("F0") ?? "0";

            var resultText = $"{emoji} *{typeName}*\n\n{sign}{amount:F0} TJS\n📂 {catName2}{descText}{impText}{limitWarning}\n\n💰 Баланс: ||{balanceText} TJS||";
            
            if (flow.PendingMessageId.HasValue)
            {
                await bot.EditMessageTextAsync(chatId, flow.PendingMessageId.Value, resultText,
                    ParseMode.Markdown, replyMarkup: BotInlineKeyboards.TransactionComplete(), cancellationToken: ct);
                return (txn.Id, flow.PendingMessageId);
            }
            else
            {
                var msg = await bot.SendTextMessageAsync(chatId, resultText,
                    ParseMode.Markdown, replyMarkup: BotInlineKeyboards.TransactionComplete(), cancellationToken: ct);
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

    // Запись транзакции с описанием (простой вызов)
    public async Task AddTransactionWithDescriptionAsync(ITelegramBotClient bot, long chatId, long userId, decimal amount, int categoryId, TransactionType type, string? description, bool isImpulsive, CancellationToken ct)
    {
        try
        {
            if (type == TransactionType.Expense)
            {
                var isBlocked = await limitService.IsCategoryBlockedAsync(userId, categoryId, ct);
                if (isBlocked)
                {
                    var category = await categoryService.GetCategoryByIdAsync(userId, categoryId, ct);
                    var catName = category != null ? $"{category.Icon} {category.Name}" : "категория";
                    await bot.SendTextMessageAsync(chatId, 
                        $"🔒 *Категория заблокирована!*\n\n{catName}\n\n_Лимит превышен._", 
                        ParseMode.Markdown, replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
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
            
            var limitWarning = await GetLimitWarningAsync(userId, categoryId, amount, type, ct);

            await bot.SendTextMessageAsync(chatId,
                $"{emoji} *{sign}{amount:F2} {account?.Currency}*\n📂 *{catName2}*{desc}{imp}{limitWarning}\n\n💰 Баланс: *{account?.Balance:F2}*",
                ParseMode.Markdown, replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
        }
        catch (Exception ex)
        {
            System.Console.WriteLine(ex);
            await bot.SendTextMessageAsync(chatId, "❌ Ошибка: " + ex.Message, replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
        }
    }

    // Получение предупреждения о лимите
    private async Task<string> GetLimitWarningAsync(long userId, int categoryId, decimal amount, TransactionType type, CancellationToken ct)
    {
        if (type != TransactionType.Expense) return "";
        
        var (limit, warningLevel) = await limitService.AddSpendingAsync(userId, categoryId, amount, ct);
        if (limit == null || warningLevel == 0) return "";
        
        var percent = limit.Amount > 0 ? (limit.SpentAmount / limit.Amount) * 100 : 0;
        return warningLevel switch
        {
            100 => $"\n\n🔴 *Лимит превышен!* ({percent:F0}%)\n_Категория заблокирована на 24 часа_",
            80 => $"\n\n⚠️ *Внимание!* Лимит на {percent:F0}%",
            50 => $"\n\n📊 Лимит на {percent:F0}%",
            _ => ""
        };
    }

    // Получение рекомендуемых категорий
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
}
