using System.Text;
using Console.Bot;
using Infrastructure.Services;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Console.Commands;

// Команда для управления целями с возможностью пополнения и удаления
public class GoalCommand(IGoalService goalService)
{
    public async Task ShowMenuAsync(ITelegramBotClient botClient, long chatId, long userId, CancellationToken ct, int? messageId = null)
    {
        var goals = await goalService.GetUserGoalsAsync(userId, ct);
        var activeGoal = await goalService.GetActiveGoalAsync(userId, ct);

        var sb = new StringBuilder();
        sb.AppendLine("🎯 *Мои Цели*\n");

        var buttons = new List<InlineKeyboardButton[]>();

        if (activeGoal != null)
        {
            var percent = activeGoal.TargetAmount > 0 ? (activeGoal.CurrentAmount / activeGoal.TargetAmount) * 100 : 0;
            sb.AppendLine($"🌟 *{activeGoal.Name}* (активная)");
            sb.AppendLine($"💰 {activeGoal.CurrentAmount:F0} / {activeGoal.TargetAmount:F0} ({percent:F0}%)");
            if (activeGoal.Deadline.HasValue)
            {
                var daysLeft = (activeGoal.Deadline.Value - DateTimeOffset.UtcNow).Days;
                sb.AppendLine($"📅 до {activeGoal.Deadline:dd.MM.yyyy} ({daysLeft} дн.)");
            }
            sb.AppendLine();

            // Кнопки для активной цели
            buttons.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData("💵 Пополнить", $"goal:deposit:{activeGoal.Id}"),
                InlineKeyboardButton.WithCallbackData("✅ Завершить", $"goal:complete:{activeGoal.Id}"),
                InlineKeyboardButton.WithCallbackData("🗑️", $"goal:delete:{activeGoal.Id}")
            });
        }
        else
        {
            sb.AppendLine("Нет активной цели.\n");
        }

        // Другие цели
        var otherGoals = goals.Where(g => !g.IsActive).Take(3).ToList();
        if (otherGoals.Any())
        {
            sb.AppendLine("*Другие цели:*");
            foreach (var g in otherGoals)
            {
                var p = g.TargetAmount > 0 ? (g.CurrentAmount / g.TargetAmount) * 100 : 0;
                sb.AppendLine($"- {g.Name}: {p:F0}%");
                
                buttons.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData($"⭐ {g.Name}", $"goal:activate:{g.Id}"),
                    InlineKeyboardButton.WithCallbackData("🗑️", $"goal:delete:{g.Id}")
                });
            }
        }

        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("➕ Создать цель", "goal:create") });
        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("🔙 Назад", "action:cancel") });

        if (messageId.HasValue)
            await botClient.EditMessageTextAsync(chatId, messageId.Value, sb.ToString(), ParseMode.Markdown, replyMarkup: new InlineKeyboardMarkup(buttons), cancellationToken: ct);
        else
            await botClient.SendTextMessageAsync(chatId, sb.ToString(), ParseMode.Markdown, replyMarkup: new InlineKeyboardMarkup(buttons), cancellationToken: ct);
    }
}
