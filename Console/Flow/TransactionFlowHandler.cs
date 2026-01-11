using Console.Bot;
using Console.Bot.Keyboards;
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

    // Внутренние категории (не показываем пользователю)
    private static readonly string[] InternalCategoryNames = 
    {
        // Долги
        "Возврат долга", "Погашение долга", "Выплата долга", "Выплата долгов",
        "← Возврат долга", "→ Выплата долга",
        // Цели
        "Из цели", "На цель", "Из целей", "Цели", "Цель",
        "→ Цели", "← Из целей",
        // Регулярные платежи
        "Регулярный платёж", "→ Регулярный платёж"
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

    #region === ВВОД СУММЫ ===

    private async Task<bool> HandleAmountAsync(ITelegramBotClient bot, long chatId, long userId, string text, UserFlowState flow, CancellationToken ct)
    {
        var parts = text.Trim().Split(' ', 2);
        if (!FlowHelper.TryParseAmount(parts[0], out var amount) || amount <= 0)
        {
            var errorText = "❌ *Неверный формат*\n\nВведите сумму числом, например: `500` или `1500 зарплата`";
            await EditOrSendAsync(bot, chatId, flow.PendingMessageId, errorText, 
                flow.PendingType == TransactionType.Expense ? TransactionKeyboards.ExpenseStart(flow.PendingIsImpulsive) : TransactionKeyboards.IncomeStart(), ct);
            return true;
        }

        flow.PendingAmount = amount;
        flow.PendingDescription = parts.Length > 1 ? parts[1].Trim() : null;
        flow.Step = UserFlowStep.ChoosingCategory;

        // Получаем топ-2 и остальные категории
        var (top2, others) = await GetSmartCategoriesAsync(userId, flow.PendingType, ct);
        
        var typeEmoji = flow.PendingType == TransactionType.Income ? "💰" : "💸";
        var typeLabel = flow.PendingType == TransactionType.Income ? "Записываем доход" : "Записываем расход";
        var descHint = flow.PendingDescription != null ? $"\n📝 _{flow.PendingDescription}_" : "";
        var prompt = $"{typeEmoji} *{typeLabel}*\n\n" +
                     $"💵 Сумма: *{amount:N0} TJS*{descHint}\n\n" +
                     $"Выберите категорию или создайте новую:";
        
        var keyboard = TransactionKeyboards.SmartCategories(top2, others, flow.PendingType);
        var catMsg = await bot.SendTextMessageAsync(chatId, prompt, ParseMode.Markdown, replyMarkup: keyboard, cancellationToken: ct);
        flow.PendingMessageId = catMsg.MessageId;
        return true;
    }

    #endregion

    #region === НОВАЯ КАТЕГОРИЯ ===

    private async Task<bool> HandleNewCategoryAsync(ITelegramBotClient bot, long chatId, long userId, string text, UserFlowState flow, CancellationToken ct)
    {
        var name = text.Length > 20 ? text[..20] : text;
        var newCat = await categoryService.CreateAsync(userId, name, flow.PendingType, "🆕", ct);

        flow.PendingCategoryId = newCat.Id;
        flow.PendingMessageId = null; // Отправить новое сообщение, не редактировать
        
        var (txnId, msgId) = await AddTransactionAsync(bot, chatId, userId, flow, ct);
        if (txnId.HasValue)
        {
            flow.PendingTransactionId = txnId;
            flow.PendingMessageId = msgId;
            flow.Step = UserFlowStep.None;
        }
        return true;
    }

    
    // Показать промпт для ввода новой категории
    public async Task ShowNewCategoryPromptAsync(ITelegramBotClient bot, long chatId, int? msgId, TransactionType type, CancellationToken ct)
    {
        var typeLabel = type == TransactionType.Income ? "дохода" : "расхода";
        var text = $"✏️ *Новая категория*\n\nВведите название для категории {typeLabel}:";
        await EditOrSendAsync(bot, chatId, msgId, text, TransactionKeyboards.NewCategoryInput(), ct);
    }

    #endregion

    #region === ОПИСАНИЕ ===

    private async Task<bool> HandleDescriptionAsync(ITelegramBotClient bot, long chatId, long userId, string text, UserFlowState flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        await AddTransactionWithDescriptionAsync(bot, chatId, userId, flow.PendingAmount, flow.PendingCategoryId!.Value, flow.PendingType, text, flow.PendingIsImpulsive, ct);
        flowDict.Remove(userId);
        return true;
    }

    #endregion

    #region === ЗАПИСЬ ТРАНЗАКЦИИ ===

    
    // Записать транзакцию и показать подтверждение
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
            if (type == TransactionType.Expense && await limitService.IsCategoryBlockedAsync(userId, categoryId, ct))
            {
                var category = await categoryService.GetCategoryByIdAsync(userId, categoryId, ct);
                var catName = category != null ? $"{category.Icon} {category.Name}" : "категория";
                var blockedText = $"🔒 *Категория заблокирована*\n\n{catName}\n\n_Лимит превышен. Попробуйте другую категорию._";
                await EditOrSendAsync(bot, chatId, flow.PendingMessageId, blockedText, BotInlineKeyboards.MainMenu(), ct);
                return (null, null);
            }

            var txn = await transactionService.ProcessTransactionAsync(userId, categoryId, amount, type, description, isImpulsive, null, ct);
            var account = await accountService.GetUserAccountAsync(userId, ct);
            var cat = await categoryService.GetCategoryByIdAsync(userId, categoryId, ct);

            var resultText = BuildTransactionConfirmText(type, amount, cat, description, isImpulsive, account?.Balance ?? 0, account?.Currency ?? "TJS");
            var limitWarning = await GetLimitWarningAsync(userId, categoryId, amount, type, ct);
            resultText += limitWarning;
            
            if (flow.PendingMessageId.HasValue)
            {
                await bot.EditMessageTextAsync(chatId, flow.PendingMessageId.Value, resultText,
                    ParseMode.Markdown, replyMarkup: TransactionKeyboards.TransactionConfirm(), cancellationToken: ct);
                return (txn.Id, flow.PendingMessageId);
            }
            else
            {
                var msg = await bot.SendTextMessageAsync(chatId, resultText,
                    ParseMode.Markdown, replyMarkup: TransactionKeyboards.TransactionConfirm(), cancellationToken: ct);
                return (txn.Id, msg.MessageId);
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine(ex);
            await bot.SendTextMessageAsync(chatId, $"❌ *Ошибка*\n\n{ex.Message}", ParseMode.Markdown, replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
            return (null, null);
        }
    }
    
    // Показать успешное сообщение после "Готово"
    public async Task ShowSuccessAsync(ITelegramBotClient bot, long chatId, int? msgId, TransactionType type, decimal amount, 
        string? categoryName, string? categoryIcon, string? description, decimal balance, CancellationToken ct)
    {
        var typeLabel = type == TransactionType.Income ? "Доход" : "Расход";
        var sign = type == TransactionType.Income ? "+" : "-";
        var catDisplay = !string.IsNullOrEmpty(categoryName) ? $"{categoryIcon} {categoryName}" : "Без категории";
        var descDisplay = !string.IsNullOrEmpty(description) ? $"📝 Описание: _{description}_\n" : "";
        
        var text = $"✅ *Успешно выполнено!*\n\n" +
                   $"━━━━━━━━━━━━━━\n" +
                   $"💰 {typeLabel}: *{sign}{amount:N0} TJS*\n" +
                   $"📂 Категория: {catDisplay}\n" +
                   $"{descDisplay}" +
                   $"💵 Баланс: *{balance:N0} TJS*\n" +
                   $"━━━━━━━━━━━━━━\n\n" +
                   $"Что дальше?";
        
        await EditOrSendAsync(bot, chatId, msgId, text, TransactionKeyboards.AfterTransaction(), ct);
    }

    
    // Показать сообщение после отмены
    public async Task ShowCancelledAsync(ITelegramBotClient bot, long chatId, int? msgId, CancellationToken ct)
    {
        var text = "🏠 *Главное меню*\n\n↩️ _Запись отменена_\n\nВыберите действие:";
        await EditOrSendAsync(bot, chatId, msgId, text, BotInlineKeyboards.MainMenu(), ct);
    }

    
    // Записать транзакцию с описанием
    public async Task AddTransactionWithDescriptionAsync(ITelegramBotClient bot, long chatId, long userId, decimal amount, int categoryId, TransactionType type, string? description, bool isImpulsive, CancellationToken ct)
    {
        try
        {
            if (type == TransactionType.Expense && await limitService.IsCategoryBlockedAsync(userId, categoryId, ct))
            {
                var category = await categoryService.GetCategoryByIdAsync(userId, categoryId, ct);
                var catName = category != null ? $"{category.Icon} {category.Name}" : "категория";
                await bot.SendTextMessageAsync(chatId, $"🔒 *Категория заблокирована*\n\n{catName}", ParseMode.Markdown, replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
                return;
            }

            await transactionService.ProcessTransactionAsync(userId, categoryId, amount, type, description, isImpulsive, null, ct);
            var account = await accountService.GetUserAccountAsync(userId, ct);
            var cat = await categoryService.GetCategoryByIdAsync(userId, categoryId, ct);

            var emoji = type == TransactionType.Income ? "✅" : "🛒";
            var sign = type == TransactionType.Income ? "+" : "-";
            var typeLabel = type == TransactionType.Income ? "Доход" : "Расход";
            var catText = cat != null ? $"{cat.Icon} {cat.Name}" : "";
            var descText = !string.IsNullOrEmpty(description) ? $"\n📝 _{description}_" : "";
            var impText = isImpulsive ? "\n🔥 _Импульсивная_" : "";
            var limitWarning = await GetLimitWarningAsync(userId, categoryId, amount, type, ct);

            var text = $"{emoji} *{typeLabel} сохранён!*\n\n" +
                       $"*{sign}{amount:N0} TJS*\n" +
                       $"📂 {catText}{descText}{impText}{limitWarning}\n\n" +
                       $"💰 Баланс: *{account?.Balance:N0} TJS*";

            await bot.SendTextMessageAsync(chatId, text, ParseMode.Markdown, replyMarkup: TransactionKeyboards.AfterTransaction(), cancellationToken: ct);
        }
        catch (Exception ex)
        {
            System.Console.WriteLine(ex);
            await bot.SendTextMessageAsync(chatId, $"❌ *Ошибка*\n\n{ex.Message}", ParseMode.Markdown, replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
        }
    }

    #endregion

    #region === ХЕЛПЕРЫ ===

    private static string BuildTransactionConfirmText(TransactionType type, decimal amount, Domain.Entities.Category? cat, string? description, bool isImpulsive, decimal balance, string currency)
    {
        var emoji = type == TransactionType.Income ? "✅" : "💲";
        var typeLabel = type == TransactionType.Income ? "Отлично! Доход записан" : "Расход записан";
        var sign = type == TransactionType.Income ? "+" : "-";
        var catText = cat != null ? $"{cat.Icon} {cat.Name}" : "Без категории";
        var descText = !string.IsNullOrEmpty(description) ? $"📝 Описание: _{description}_\n" : "";
        var impText = isImpulsive ? "\n🔥 _Импульсивная_" : "";

        return $"{emoji} *{typeLabel}*\n\n" +
               $"━━━━━━━━━━━━━━\n" +
               $"💵 Сумма: *{sign}{amount:N0} {currency}*\n" +
               $"📂 Категория: {catText}\n" +
               $"{descText}{impText}" +
               $"💰 Баланс: *{balance:N0} {currency}*\n" +
               $"━━━━━━━━━━━━━━\n\n" +
               $"Подтвердите или отмените:";
    }

    private async Task<string> GetLimitWarningAsync(long userId, int categoryId, decimal amount, TransactionType type, CancellationToken ct)
    {
        if (type != TransactionType.Expense) return "";
        
        var (limit, warningLevel) = await limitService.AddSpendingAsync(userId, categoryId, amount, ct);
        if (limit == null || warningLevel == 0) return "";
        
        var percent = limit.Amount > 0 ? (limit.SpentAmount / limit.Amount) * 100 : 0;
        return warningLevel switch
        {
            100 => $"\n\n🔴 *Лимит превышен!* ({percent:F0}%)",
            80 => $"\n\n⚠️ *Внимание!* Лимит: {percent:F0}%",
            50 => $"\n\n📊 Лимит: {percent:F0}%",
            _ => ""
        };
    }

    // Получить топ-2 и остальные категории (без внутренних, отсортированные по дате использования)
    private async Task<(IReadOnlyList<Domain.Entities.Category> Top2, IReadOnlyList<Domain.Entities.Category> Others)> GetSmartCategoriesAsync(long userId, TransactionType type, CancellationToken ct)
    {
        // Все ID категорий отсортированные по дате последнего использования
        var recentIds = await transactionService.GetRecentCategoryIdsAsync(userId, type, 100, ct);
        
        var all = await categoryService.GetUserCategoriesAsync(userId, ct);
        
        if (!all.Any())
        {
            await categoryService.InitializeDefaultCategoriesAsync(userId, ct);
            all = await categoryService.GetUserCategoriesAsync(userId, ct);
        }

        // Фильтруем: только нужный тип, активные, без внутренних
        var relevant = all.Where(c => c.Type == type && c.IsActive && !InternalCategoryNames.Contains(c.Name)).ToList();
        
        // Топ-2: первые 2 из часто используемых (только из отфильтрованных)
        var relevantIds = relevant.Select(c => c.Id).ToHashSet();
        var filteredRecentIds = recentIds.Where(id => relevantIds.Contains(id)).ToList();
        
        var top2 = new List<Domain.Entities.Category>();
        foreach (var id in filteredRecentIds.Take(2))
        {
            var c = relevant.FirstOrDefault(x => x.Id == id);
            if (c != null) top2.Add(c);
        }

        // Остальные: отсортированы по дате использования (без топ-2)
        var top2Ids = top2.Select(c => c.Id).ToHashSet();
        var recentIdsList = recentIds.ToList();
        
        // Сортируем: сначала те, что есть в recentIds (по их порядку), потом остальные
        var others = relevant
            .Where(c => !top2Ids.Contains(c.Id))
            .OrderBy(c => {
                var idx = recentIdsList.IndexOf(c.Id);
                return idx >= 0 ? idx : int.MaxValue;
            })
            .ToList();

        return (top2, others);
    }



    
    // Получение всех категорий (для пагинации и legacy)
    public async Task<(IReadOnlyList<Domain.Entities.Category> Top2, IReadOnlyList<Domain.Entities.Category> Others)> GetCategoriesAsync(long userId, TransactionType type, CancellationToken ct)
    {
        return await GetSmartCategoriesAsync(userId, type, ct);
    }

    private static async Task EditOrSendAsync(ITelegramBotClient bot, long chatId, int? msgId, string text, Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup keyboard, CancellationToken ct)
    {
        if (msgId.HasValue)
        {
            try
            {
                await bot.EditMessageTextAsync(chatId, msgId.Value, text, ParseMode.Markdown, replyMarkup: keyboard, cancellationToken: ct);
            }
            catch
            {
                await bot.SendTextMessageAsync(chatId, text, ParseMode.Markdown, replyMarkup: keyboard, cancellationToken: ct);
            }
        }
        else
        {
            await bot.SendTextMessageAsync(chatId, text, ParseMode.Markdown, replyMarkup: keyboard, cancellationToken: ct);
        }
    }

    #endregion
}
