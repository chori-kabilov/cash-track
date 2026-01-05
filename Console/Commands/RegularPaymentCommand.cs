using System.Text;
using Console.Bot;
using Domain.Enums;
using Infrastructure.Services;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Console.Commands;

public class RegularPaymentCommand(IRegularPaymentService regularPaymentService)
{
    public async Task ShowMenuAsync(ITelegramBotClient botClient, long chatId, long userId, CancellationToken cancellationToken, int? messageId = null)
    {
        var payments = await regularPaymentService.GetUserPaymentsAsync(userId, cancellationToken);
        
        var sb = new StringBuilder();
        sb.AppendLine("🔄 *Регулярные платежи*\n");

        if (!payments.Any())
        {
            sb.AppendLine("Нет регулярных платежей. Настройте, чтобы не забыть! 📅\n");
        }
        else
        {
            foreach (var p in payments)
            {
                var status = !p.IsPaused ? "✅" : "⏸️";
                var nextDate = p.NextDueDate.HasValue ? p.NextDueDate.Value.ToString("dd.MM.yyyy") : "—";
                var freq = p.Frequency switch 
                {
                    PaymentFrequency.Daily => "Ежедневно",
                    PaymentFrequency.Weekly => "Еженедельно",
                    PaymentFrequency.Monthly => "Ежемесячно",
                    PaymentFrequency.Yearly => "Ежегодно",
                    _ => "Другое"
                };

                sb.AppendLine($"{status} *{p.Name}* ({p.Amount:F2})");
                sb.AppendLine($"🔁 {freq}, след: {nextDate}");
                sb.AppendLine($"/pay\\_regular\\_{p.Id}");
                sb.AppendLine();
            }
        }

        var buttons = new InlineKeyboardMarkup(
            new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("➕ Создать платеж", "regular:create") },
                new[] { InlineKeyboardButton.WithCallbackData("🔙 Назад", "action:cancel") }
            });

        if (messageId.HasValue)
            await botClient.EditMessageTextAsync(chatId, messageId.Value, sb.ToString(), ParseMode.Markdown, replyMarkup: buttons, cancellationToken: cancellationToken);
        else
            await botClient.SendTextMessageAsync(chatId, sb.ToString(), ParseMode.Markdown, replyMarkup: buttons, cancellationToken: cancellationToken);
    }
}
