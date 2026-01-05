using Console.Bot;
using Domain.Enums;
using Infrastructure.Services;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace Console.Commands;

public class StatsCommand(
    IAccountService accountService,
    ITransactionService transactionService,
    IRegularPaymentService regularPaymentService)
{
    public async Task ExecuteAsync(ITelegramBotClient botClient, long chatId, long userId, CancellationToken cancellationToken, int? messageId = null)
    {
        var account = await accountService.GetUserAccountAsync(userId, cancellationToken)
                      ?? await accountService.CreateAccountAsync(userId, cancellationToken: cancellationToken);

        var currency = account.Currency;
        var nowUtc = DateTimeOffset.UtcNow;
        var monthStartUtc = new DateTimeOffset(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, TimeSpan.Zero);

        var totalIncome = await transactionService.GetTotalIncomeAsync(userId, monthStartUtc, cancellationToken);
        var totalExpense = await transactionService.GetTotalExpenseAsync(userId, monthStartUtc, cancellationToken);
        
        // Top Expenses
        var topExpenses = await transactionService.GetTopExpensesAsync(userId, monthStartUtc, 3, cancellationToken);
        var forecastExpense = (await regularPaymentService.GetUserPaymentsAsync(userId, cancellationToken))
            .Where(p => !p.IsPaused && p.NextDueDate.HasValue && p.NextDueDate < nowUtc.AddDays(30)).Sum(p => p.Amount);

        var message = $"📊 *Статистика за {nowUtc:MMMM yyyy}*\n\n" +
                      $"📥 Доход: *+{totalIncome:F2} {currency}*\n" +
                      $"📤 Расход: *-{totalExpense:F2} {currency}*\n" +
                      $"💼 Итог: *{totalIncome - totalExpense:F2} {currency}*\n\n";

        if (topExpenses.Any())
        {
            message += "🏆 *Куда уходят деньги?*\n";
            foreach (var (cat, amt) in topExpenses)
            {
                var percent = totalExpense > 0 ? (amt / totalExpense) * 100 : 0;
                message += $"{cat.Icon} {cat.Name}: {amt:F2} ({percent:F0}%)\n";
            }
            message += "\n";
        }

        message += $"🔮 *Прогноз на месяц:*\n" +
                   $"Списания (регулярные): {forecastExpense:F2} {currency}\n\n";

        var recentTransactions = await transactionService.GetUserTransactionsAsync(userId, 5, cancellationToken);
        
        if (recentTransactions.Any())
        {
            message += "🕰 *Последние операции:*\n";
            foreach (var t in recentTransactions)
            {
                var sign = t.Type == TransactionType.Income ? "+" : "-";
                var icon = t.Type == TransactionType.Income ? "💰" : "💸";
                message += $"{icon} {sign}{t.Amount:F2} {currency} ({t.Date:dd.MM})\n";
            }
        }
        
        message += $"\n_{BotPersonality.GetRandomTip()}_";

        if (messageId.HasValue)
        {
            await botClient.EditMessageTextAsync(chatId, messageId.Value, message, ParseMode.Markdown, replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: cancellationToken);
        }
        else
        {
            await botClient.SendTextMessageAsync(chatId, message, ParseMode.Markdown, replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: cancellationToken);
        }
    }
}
