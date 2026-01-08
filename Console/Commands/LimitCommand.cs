using System.Text;
using Console.Bot;
using Infrastructure.Services;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Console.Commands;

// Команда для управления лимитами расходов с возможностью удаления
public class LimitCommand(ILimitService limitService, ICategoryService categoryService)
{
    public async Task ShowMenuAsync(ITelegramBotClient botClient, long chatId, long userId, CancellationToken ct, int? messageId = null)
    {
        var limits = await limitService.GetUserLimitsAsync(userId, ct);
        
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
            foreach (var l in limits)
            {
                var percent = l.Amount > 0 ? (l.SpentAmount / l.Amount) * 100 : 0;
                var status = percent >= 100 ? "🔴" : percent >= 80 ? "⚠️" : "✅";
                var catIcon = l.Category?.Icon ?? "📂";
                var catName = l.Category?.Name ?? "Без категории";
                var blockedText = l.IsBlocked ? " 🔒" : "";
                
                sb.AppendLine($"{status} {catIcon} *{catName}*{blockedText}");
                sb.AppendLine($"   {l.SpentAmount:F0} / {l.Amount:F0} ({percent:F0}%)\n");

                // Кнопки для каждого лимита
                buttons.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData($"{catIcon} {catName}", $"limit:info:{l.Id}"),
                    InlineKeyboardButton.WithCallbackData("🗑️", $"limit:delete:{l.Id}")
                });
            }
            
            var exceeded = limits.Count(l => l.SpentAmount >= l.Amount);
            var warning = limits.Count(l => l.SpentAmount >= l.Amount * 0.8m && l.SpentAmount < l.Amount);
            
            if (exceeded > 0)
                sb.AppendLine($"🔴 Превышено: {exceeded}");
            if (warning > 0)
                sb.AppendLine($"⚠️ Внимание: {warning}");
        }

        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("➕ Создать лимит", "limit:create") });
        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("🔄 Сбросить месячные", "limit:reset") });
        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("🔙 Назад", "action:cancel") });

        if (messageId.HasValue)
            await botClient.EditMessageTextAsync(chatId, messageId.Value, sb.ToString(), ParseMode.Markdown, replyMarkup: new InlineKeyboardMarkup(buttons), cancellationToken: ct);
        else
            await botClient.SendTextMessageAsync(chatId, sb.ToString(), ParseMode.Markdown, replyMarkup: new InlineKeyboardMarkup(buttons), cancellationToken: ct);
    }

    // Показать категории для создания лимита
    public async Task ShowCategoriesAsync(ITelegramBotClient botClient, long chatId, long userId, CancellationToken ct)
    {
        var categories = await categoryService.GetUserCategoriesAsync(userId, ct);
        var expenseCategories = categories.Where(c => c.Type == Domain.Enums.TransactionType.Expense && c.IsActive).ToList();
        
        if (!expenseCategories.Any())
        {
            await botClient.SendTextMessageAsync(chatId, "❌ Нет категорий расходов.", replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
            return;
        }

        var buttons = expenseCategories
            .Select(c => new[] { InlineKeyboardButton.WithCallbackData($"{c.Icon} {c.Name}", $"limit:cat:{c.Id}") })
            .ToList();
        
        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("Отмена", "action:cancel") });

        await botClient.SendTextMessageAsync(chatId, "Выберите категорию для лимита:", replyMarkup: new InlineKeyboardMarkup(buttons), cancellationToken: ct);
    }
}
