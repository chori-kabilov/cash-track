using Console.Bot;
using Console.Flow;
using Infrastructure.Services;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace Console.Commands;

// Панель управления балансом
public class BalanceCommand(
    IAccountService accountService, 
    IGoalService goalService, 
    IDebtService debtService, 
    IRegularPaymentService regularPaymentService,
    ITransactionService transactionService)
{
    // Показать dashboard с расчётом свободных средств
    public async Task ExecuteAsync(
        ITelegramBotClient botClient, 
        long chatId, 
        long userId, 
        UserFlowState? flowState,
        CancellationToken ct, 
        int? messageId = null)
    {
        var account = await accountService.GetUserAccountAsync(userId, ct)
                      ?? await accountService.CreateAccountAsync(userId, ct: ct);

        var goals = await goalService.GetUserGoalsAsync(userId, ct);
        var debts = await debtService.GetUnpaidDebtsAsync(userId, ct);
        var payments = await regularPaymentService.GetActiveAsync(userId, ct);

        // Расчёт сумм
        decimal totalBalance = account.Balance;
        decimal goalsSavings = goals.Sum(g => g.CurrentAmount);
        decimal paymentsAmount = payments.Sum(p => p.Amount);
        decimal debtsIOweMoney = debts.Where(d => d.Type == Domain.Enums.DebtType.IOwe).Sum(d => d.RemainingAmount);
        decimal debtsTheyOweMe = debts.Where(d => d.Type == Domain.Enums.DebtType.TheyOwe).Sum(d => d.RemainingAmount);
        decimal netDebt = debtsTheyOweMe - debtsIOweMoney;

        // Состояния переключателей
        bool showDebts = flowState?.BalanceShowDebts ?? false;
        bool showGoals = flowState?.BalanceShowGoals ?? true;
        bool showPayments = flowState?.BalanceShowPayments ?? true;

        // Расчёт свободных средств
        decimal freeAmount = totalBalance;
        if (showGoals) freeAmount -= goalsSavings;
        if (showPayments) freeAmount -= paymentsAmount;
        if (showDebts) freeAmount += netDebt;

        // Прогноз на сколько дней хватит
        var avgExpense = await GetAverageDailyExpenseAsync(userId, ct);
        var daysRemaining = avgExpense > 0 ? (int)(freeAmount / avgExpense) : 999;
        var daysText = daysRemaining > 0 ? $"{daysRemaining} дней" : "< 1 дня";

        var freeEmoji = freeAmount < 0 ? "⚠️" : "💸";
        var freeColor = freeAmount < 0 ? "🔴" : "";
        
        var text = $"💰 *Твой Капитал*\n\n" +
                   $"💵 *В наличии:* ||{totalBalance:F0} {account.Currency}||\n" +
                   $"{freeEmoji} *Свободно:* {freeColor}*{freeAmount:F0} {account.Currency}*\n\n" +
                   $"🔻 *Удержано:*\n" +
                   $"  📅 Регулярные: {(showPayments ? $"-{paymentsAmount:F0}" : "_не учтены_")}\n" +
                   $"  🎯 Цели: {(showGoals ? $"-{goalsSavings:F0}" : "_не учтены_")}\n" +
                   $"  📉 Долги: {(showDebts ? $"{netDebt:F0}" : "_не учтены_")}\n\n" +
                   $"🔄 *Прогноз:* Денег хватит на *{daysText}*.";

        var keyboard = BotInlineKeyboards.BalanceDashboard(showDebts, showGoals, showPayments);

        if (messageId.HasValue)
            await botClient.EditMessageTextAsync(chatId, messageId.Value, text, ParseMode.Markdown, replyMarkup: keyboard, cancellationToken: ct);
        else
            await botClient.SendTextMessageAsync(chatId, text, ParseMode.Markdown, replyMarkup: keyboard, cancellationToken: ct);
    }

    // Средний расход в день за последние 30 дней
    private async Task<decimal> GetAverageDailyExpenseAsync(long userId, CancellationToken ct)
    {
        var expenses = await transactionService.GetExpensesByPeriodAsync(userId, 
            DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow, ct);
        
        if (!expenses.Any()) return 0;
        
        return expenses.Sum(e => e.Amount) / 30;
    }
}
