using System.Text;
using Console.Bot;
using Domain.Enums;
using Infrastructure.Services;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Console.Commands;

// Команда для управления долгами с возможностью оплаты и удаления
public class DebtCommand(IDebtService debtService)
{
    public async Task ShowMenuAsync(ITelegramBotClient botClient, long chatId, long userId, CancellationToken ct, int? messageId = null)
    {
        var debts = await debtService.GetUserDebtsAsync(userId, ct);
        
        var sb = new StringBuilder();
        sb.AppendLine("🤝 *Долговая книга*\n");

        var buttons = new List<InlineKeyboardButton[]>();

        if (!debts.Any())
        {
            sb.AppendLine("У вас нет долгов. Чистота и порядок! ✨\n");
        }
        else
        {
            var activeDebts = debts.Where(d => !d.IsPaid).OrderByDescending(d => d.DueDate.HasValue && d.DueDate < DateTimeOffset.UtcNow).ThenBy(d => d.DueDate).ToList();
            
            foreach (var d in activeDebts)
            {
                var icon = d.Type == DebtType.IOwe ? "🔴" : "🟢";
                var overdue = d.DueDate.HasValue && d.DueDate < DateTimeOffset.UtcNow ? "⚠️" : "";
                var date = d.DueDate.HasValue ? $"до {d.DueDate:dd.MM}" : "";
                var paid = d.Amount - d.RemainingAmount;
                
                sb.AppendLine($"{overdue}{icon} *{d.PersonName}*");
                sb.AppendLine($"💰 {paid:F0} / {d.Amount:F0} {date}\n");

                // Кнопки для каждого долга
                buttons.Add(new[]
                {
                    InlineKeyboardButton.WithCallbackData($"💵 Оплатить", $"debt:pay:{d.Id}"),
                    InlineKeyboardButton.WithCallbackData("✅ Закрыть", $"debt:close:{d.Id}"),
                    InlineKeyboardButton.WithCallbackData("🗑️", $"debt:delete:{d.Id}")
                });
            }
        }

        buttons.Add(new[] 
        { 
            InlineKeyboardButton.WithCallbackData("🔴 Я должен", "debt:create:i_owe"), 
            InlineKeyboardButton.WithCallbackData("🟢 Мне должны", "debt:create:they_owe") 
        });
        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("🔙 Назад", "action:cancel") });

        if (messageId.HasValue)
            await botClient.EditMessageTextAsync(chatId, messageId.Value, sb.ToString(), ParseMode.Markdown, replyMarkup: new InlineKeyboardMarkup(buttons), cancellationToken: ct);
        else
            await botClient.SendTextMessageAsync(chatId, sb.ToString(), ParseMode.Markdown, replyMarkup: new InlineKeyboardMarkup(buttons), cancellationToken: ct);
    }
}
