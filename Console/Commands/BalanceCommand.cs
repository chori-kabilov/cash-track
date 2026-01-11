using Console.Bot;
using Console.Flow;
using Domain.Enums;
using Infrastructure.Services;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace Console.Commands;

/// <summary>
/// Панель управления балансом — показывает свободные средства и прогноз
/// </summary>
public class BalanceCommand(
    IAccountService accountService, 
    IGoalService goalService, 
    IDebtService debtService, 
    IRegularPaymentService regularPaymentService,
    ITransactionService transactionService)
{
    private const int ForecastDays = 30;
    private const string DefaultCurrency = "TJS";

    #region === PUBLIC METHODS ===

    /// <summary>
    /// Показать дашборд баланса с расчётом свободных средств
    /// </summary>
    public async Task ExecuteAsync(
        ITelegramBotClient bot, 
        long chatId, 
        long userId, 
        UserFlowState? flowState,
        CancellationToken ct, 
        int? msgId = null)
    {
        // Загружаем данные
        var account = await accountService.GetUserAccountAsync(userId, ct)
                      ?? await accountService.CreateAccountAsync(userId, ct: ct);
        var goals = await goalService.GetUserGoalsAsync(userId, ct);
        var debts = await debtService.GetUnpaidDebtsAsync(userId, ct);
        var payments = await regularPaymentService.GetActiveAsync(userId, ct);

        // Расчёт сумм
        var balanceData = CalculateBalanceData(account.Balance, goals, debts, payments, flowState);

        // Прогноз
        var avgExpense = await GetAverageDailyExpenseAsync(userId, ct);
        var daysRemaining = CalculateDaysRemaining(balanceData.FreeAmount, avgExpense);

        // Формируем текст
        var text = BuildBalanceText(balanceData, daysRemaining, account.Currency ?? DefaultCurrency);
        var keyboard = BotInlineKeyboards.BalanceDashboard(
            balanceData.ShowDebts, balanceData.ShowGoals, balanceData.ShowPayments);

        await CommandHelpers.SendOrEditAsync(bot, chatId, msgId, text, keyboard, ct);
    }

    #endregion

    #region === PRIVATE METHODS ===

    private static BalanceData CalculateBalanceData(
        decimal totalBalance, 
        IReadOnlyList<Domain.Entities.Goal> goals,
        IReadOnlyList<Domain.Entities.Debt> debts,
        IReadOnlyList<Domain.Entities.RegularPayment> payments,
        UserFlowState? flowState)
    {
        var goalsSavings = goals.Sum(g => g.CurrentAmount);
        var paymentsAmount = payments.Sum(p => p.Amount);
        var debtsIOwe = debts.Where(d => d.Type == DebtType.IOwe).Sum(d => d.RemainingAmount);
        var debtsTheyOwe = debts.Where(d => d.Type == DebtType.TheyOwe).Sum(d => d.RemainingAmount);
        var netDebt = debtsTheyOwe - debtsIOwe;

        // Состояния переключателей
        var showDebts = flowState?.BalanceShowDebts ?? false;
        var showGoals = flowState?.BalanceShowGoals ?? true;
        var showPayments = flowState?.BalanceShowPayments ?? true;

        // Расчёт свободных средств
        var freeAmount = totalBalance;
        if (showGoals) freeAmount -= goalsSavings;
        if (showPayments) freeAmount -= paymentsAmount;
        if (showDebts) freeAmount += netDebt;

        return new BalanceData(totalBalance, freeAmount, goalsSavings, paymentsAmount, netDebt,
            showDebts, showGoals, showPayments);
    }

    private async Task<decimal> GetAverageDailyExpenseAsync(long userId, CancellationToken ct)
    {
        var expenses = await transactionService.GetExpensesByPeriodAsync(userId, 
            DateTimeOffset.UtcNow.AddDays(-ForecastDays), DateTimeOffset.UtcNow, ct);
        
        return expenses.Any() ? expenses.Sum(e => e.Amount) / ForecastDays : 0;
    }

    private static int CalculateDaysRemaining(decimal freeAmount, decimal avgExpense)
    {
        return avgExpense > 0 ? Math.Max(0, (int)(freeAmount / avgExpense)) : 999;
    }

    private static string BuildBalanceText(BalanceData data, int daysRemaining, string currency)
    {
        var daysText = daysRemaining > 0 ? $"{daysRemaining} дней" : "< 1 дня";
        var freeEmoji = data.FreeAmount < 0 ? "⚠️" : "💸";
        var freeColor = data.FreeAmount < 0 ? "🔴" : "";

        return $"💰 *Твой Капитал*\n\n" +
               $"💵 *В наличии:* ||{data.TotalBalance:F0} {currency}||\n" +
               $"{freeEmoji} *Свободно:* {freeColor}*{data.FreeAmount:F0} {currency}*\n\n" +
               $"🔻 *Удержано:*\n" +
               $"  📅 Регулярные: {(data.ShowPayments ? $"-{data.PaymentsAmount:F0}" : "_не учтены_")}\n" +
               $"  🎯 Цели: {(data.ShowGoals ? $"-{data.GoalsSavings:F0}" : "_не учтены_")}\n" +
               $"  📉 Долги: {(data.ShowDebts ? $"{data.NetDebt:F0}" : "_не учтены_")}\n\n" +
               $"🔄 *Прогноз:* Денег хватит на *{daysText}*.";
    }

    #endregion

    #region === NESTED TYPES ===

    private sealed record BalanceData(
        decimal TotalBalance,
        decimal FreeAmount,
        decimal GoalsSavings,
        decimal PaymentsAmount,
        decimal NetDebt,
        bool ShowDebts,
        bool ShowGoals,
        bool ShowPayments);

    #endregion
}
