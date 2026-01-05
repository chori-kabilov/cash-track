using System.Text;
using Console.Bot;
using Domain.Enums;
using Infrastructure.Services;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Console.Commands;

public class DebtCommand(IDebtService debtService)
{
    public async Task ShowMenuAsync(ITelegramBotClient botClient, long chatId, long userId, CancellationToken cancellationToken, int? messageId = null)
    {
        var debts = await debtService.GetUserDebtsAsync(userId, cancellationToken);
        
        var sb = new StringBuilder();
        sb.AppendLine("🤝 *Долговая книга*\n");

        if (!debts.Any())
        {
            sb.AppendLine("У вас нет долгов. Чистота и порядок! ✨\n");
        }
        else
        {
            var activeDebts = debts.Where(d => !d.IsPaid).OrderByDescending(d => d.DueDate.HasValue && d.DueDate < DateTimeOffset.UtcNow).ThenBy(d => d.DueDate).ToList();
            
            foreach (var d in activeDebts)
            {
                var icon = d.Type == DebtType.IOwe ? "🔴 Должен" : "🟢 Мне должны";
                var overdue = d.DueDate.HasValue && d.DueDate < DateTimeOffset.UtcNow ? "⚠️ *Просрочено* " : "";
                var date = d.DueDate.HasValue ? $"до {d.DueDate:dd.MM}" : "";
                
                sb.AppendLine($"{overdue}{icon} *{d.PersonName}*");
                sb.AppendLine($"💰 {d.Amount - d.RemainingAmount:F2} / {d.Amount:F2} {date}");
                sb.AppendLine($"/pay\\_debt\\_{d.Id}"); 
                sb.AppendLine();
            }
        }

        var buttons = new InlineKeyboardMarkup(
            new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("🔴 Я должен", "debt:create:i_owe"), InlineKeyboardButton.WithCallbackData("🟢 Мне должны", "debt:create:they_owe") },
                new[] { InlineKeyboardButton.WithCallbackData("🔙 Назад", "action:cancel") }
            });

        if (messageId.HasValue)
            await botClient.EditMessageTextAsync(chatId, messageId.Value, sb.ToString(), ParseMode.Markdown, replyMarkup: buttons, cancellationToken: cancellationToken);
        else
            await botClient.SendTextMessageAsync(chatId, sb.ToString(), ParseMode.Markdown, replyMarkup: buttons, cancellationToken: cancellationToken);
    }
}
