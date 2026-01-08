using System.Text;
using Console.Bot;
using Domain.Enums;
using Infrastructure.Services;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Console.Commands;

// Команда для управления регулярными платежами с возможностью паузы и удаления
public class RegularPaymentCommand(IRegularPaymentService regularPaymentService)
{
    public async Task ShowMenuAsync(ITelegramBotClient botClient, long chatId, long userId, CancellationToken ct, int? messageId = null)
    {
        var payments = await regularPaymentService.GetUserPaymentsAsync(userId, ct);
        
        var sb = new StringBuilder();
        sb.AppendLine("🔄 *Регулярные платежи*\n");

        var buttons = new List<InlineKeyboardButton[]>();

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
                sb.AppendLine($"🔁 {freq}, след: {nextDate}\n");

                // Кнопки для каждого платежа
                var pauseBtn = p.IsPaused 
                    ? InlineKeyboardButton.WithCallbackData("▶️", $"regular:resume:{p.Id}")
                    : InlineKeyboardButton.WithCallbackData("⏸️", $"regular:pause:{p.Id}");
                
                buttons.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData($"💳 {p.Name}", $"regular:pay:{p.Id}"),
                    pauseBtn,
                    InlineKeyboardButton.WithCallbackData("🗑️", $"regular:delete:{p.Id}")
                });
            }
        }

        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("➕ Создать платеж", "regular:create") });
        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("🔙 Назад", "action:cancel") });

        if (messageId.HasValue)
            await botClient.EditMessageTextAsync(chatId, messageId.Value, sb.ToString(), ParseMode.Markdown, replyMarkup: new InlineKeyboardMarkup(buttons), cancellationToken: ct);
        else
            await botClient.SendTextMessageAsync(chatId, sb.ToString(), ParseMode.Markdown, replyMarkup: new InlineKeyboardMarkup(buttons), cancellationToken: ct);
    }
}
