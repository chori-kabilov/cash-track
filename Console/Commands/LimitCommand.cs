using System.Text;
using Console.Bot;
using Domain.Enums;
using Infrastructure.Services;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Console.Commands;


// Управление лимитами расходов по категориям
public class LimitCommand(ILimitService limitService, ICategoryService categoryService)
{
    #region === PUBLIC METHODS ===

    
    // Показать меню лимитов
    public async Task ShowMenuAsync(ITelegramBotClient bot, long chatId, long userId, CancellationToken ct, int? msgId = null)
    {
        var limits = await limitService.GetUserLimitsAsync(userId, ct);
        var (text, buttons) = BuildLimitsMenu(limits);

        if (msgId.HasValue)
            await bot.EditMessageTextAsync(chatId, msgId.Value, text, ParseMode.Markdown, 
                replyMarkup: new InlineKeyboardMarkup(buttons), cancellationToken: ct);
        else
            await bot.SendTextMessageAsync(chatId, text, ParseMode.Markdown, 
                replyMarkup: new InlineKeyboardMarkup(buttons), cancellationToken: ct);
    }

    
    // Показать категории для создания лимита
    public async Task ShowCategoriesAsync(ITelegramBotClient bot, long chatId, long userId, CancellationToken ct)
    {
        var categories = await categoryService.GetUserCategoriesAsync(userId, ct);
        var expenseCategories = categories
            .Where(c => c.Type == TransactionType.Expense && c.IsActive)
            .ToList();
        
        if (!expenseCategories.Any())
        {
            await bot.SendTextMessageAsync(chatId, "❌ Нет категорий расходов.", 
                replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
            return;
        }

        var buttons = expenseCategories
            .Select(c => new[] { InlineKeyboardButton.WithCallbackData($"{c.Icon} {c.Name}", $"limit:cat:{c.Id}") })
            .Append(new[] { InlineKeyboardButton.WithCallbackData("Отмена", "action:cancel") })
            .ToArray();

        await bot.SendTextMessageAsync(chatId, "Выберите категорию для лимита:", 
            replyMarkup: new InlineKeyboardMarkup(buttons), cancellationToken: ct);
    }

    #endregion

    #region === PRIVATE METHODS ===

    private static (string Text, List<InlineKeyboardButton[]> Buttons) BuildLimitsMenu(
        IReadOnlyList<Domain.Entities.Limit> limits)
    {
        var sb = new StringBuilder();
        sb.AppendLine("📉 *Лимиты расходов*\n");

        var buttons = new List<InlineKeyboardButton[]>();

        if (!limits.Any())
        {
            sb.AppendLine("Нет установленных лимитов.\n");
            sb.AppendLine("_Лимиты помогают контролировать расходы по категориям._\n");
        }
        else
        {
            foreach (var limit in limits)
            {
                AppendLimitInfo(sb, buttons, limit);
            }
            AppendLimitSummary(sb, limits);
        }

        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("➕ Создать лимит", "limit:create") });
        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("🔄 Сбросить месячные", "limit:reset") });
        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("🔙 Назад", "action:cancel") });

        return (sb.ToString(), buttons);
    }

    private static void AppendLimitInfo(StringBuilder sb, List<InlineKeyboardButton[]> buttons, Domain.Entities.Limit limit)
    {
        var percent = limit.Amount > 0 ? (limit.SpentAmount / limit.Amount) * 100 : 0;
        var status = CommandHelpers.GetStatusEmoji(percent);
        var catIcon = limit.Category?.Icon ?? "📂";
        var catName = limit.Category?.Name ?? "Без категории";
        var blockedText = limit.IsBlocked ? " 🔒" : "";
        
        sb.AppendLine($"{status} {catIcon} *{catName}*{blockedText}");
        sb.AppendLine($"   {limit.SpentAmount:F0} / {limit.Amount:F0} ({percent:F0}%)\n");

        buttons.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData($"{catIcon} {catName}", $"limit:info:{limit.Id}"),
            InlineKeyboardButton.WithCallbackData("🗑️", $"limit:delete:{limit.Id}")
        });
    }

    private static void AppendLimitSummary(StringBuilder sb, IReadOnlyList<Domain.Entities.Limit> limits)
    {
        var exceeded = limits.Count(l => l.SpentAmount >= l.Amount);
        var warning = limits.Count(l => l.SpentAmount >= l.Amount * 0.8m && l.SpentAmount < l.Amount);
        
        if (exceeded > 0) sb.AppendLine($"🔴 Превышено: {exceeded}");
        if (warning > 0) sb.AppendLine($"⚠️ Внимание: {warning}");
    }

    #endregion
}
