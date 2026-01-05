using System.Text;
using Console.Bot;
using Infrastructure.Services;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Console.Commands;

public class GoalCommand(IGoalService goalService)
{
    public async Task ShowMenuAsync(ITelegramBotClient botClient, long chatId, long userId, CancellationToken cancellationToken, int? messageId = null)
    {
        var goals = await goalService.GetUserGoalsAsync(userId, cancellationToken);
        var activeGoal = await goalService.GetActiveGoalAsync(userId, cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("🎯 *Мои Цели*\n");

        if (activeGoal != null)
        {
            var percent = activeGoal.TargetAmount > 0 ? (activeGoal.CurrentAmount / activeGoal.TargetAmount) * 100 : 0;
            sb.AppendLine($"🌟 *Активная цель:* {activeGoal.Name}");
            sb.AppendLine($"💰 {activeGoal.CurrentAmount:F2} / {activeGoal.TargetAmount:F2} ({percent:F1}%)");
            if (activeGoal.Deadline.HasValue)
            {
                var daysLeft = (activeGoal.Deadline.Value - DateTimeOffset.UtcNow).Days;
                sb.AppendLine($"📅 Дедлайн: {activeGoal.Deadline:dd.MM.yyyy} (осталось {daysLeft} дн.)");
            }
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("Нет активной цели. Выберите или создайте новую.\n");
        }

        if (goals.Any(g => !g.IsActive))
        {
            sb.AppendLine("*Другие цели:*");
            foreach (var g in goals.Where(g => !g.IsActive))
            {
                sb.AppendLine($"- {g.Name}: {g.CurrentAmount:F2} / {g.TargetAmount:F2}");
            }
        }

        var buttons = new List<InlineKeyboardButton[]>
        {
            new[] { InlineKeyboardButton.WithCallbackData("➕ Создать цель", "goal:create") }
        };

        if (activeGoal != null)
        {
            buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("💵 Пополнить активную", $"goal:deposit:{activeGoal.Id}") });
        }

        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("🔙 Назад", "action:cancel") });

        if (messageId.HasValue)
            await botClient.EditMessageTextAsync(chatId, messageId.Value, sb.ToString(), ParseMode.Markdown, replyMarkup: new InlineKeyboardMarkup(buttons), cancellationToken: cancellationToken);
        else
            await botClient.SendTextMessageAsync(chatId, sb.ToString(), ParseMode.Markdown, replyMarkup: new InlineKeyboardMarkup(buttons), cancellationToken: cancellationToken);
    }
}
