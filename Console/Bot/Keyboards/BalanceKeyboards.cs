using Telegram.Bot.Types.ReplyMarkups;

namespace Console.Bot.Keyboards;

public static class BalanceKeyboards
{
    // Баланс — панель с переключателями
    public static InlineKeyboardMarkup BalanceDashboard(bool showDebts, bool showGoals, bool showPayments)
    {
        // Кнопки переключателей (одинаковая ширина для стабильности UI)
        var debtsText = showDebts ? "🟢 Долги" : "🔴 Долги";
        var goalsText = showGoals ? "🟢 Цели" : "🔴 Цели";
        var paymentsText = showPayments ? "🟢 Платежи" : "🔴 Платежи";

        return new InlineKeyboardMarkup(
            new[]
            {
                new[] 
                { 
                    InlineKeyboardButton.WithCallbackData(debtsText, "bal:toggle_debts"),
                    InlineKeyboardButton.WithCallbackData(goalsText, "bal:toggle_goals"),
                    InlineKeyboardButton.WithCallbackData(paymentsText, "bal:toggle_payments")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🔙 Назад", "bal:back"),
                    InlineKeyboardButton.WithCallbackData("📄 История", "bal:details")
                }
            });
    }

    // Баланс — деталі (только кнопка назад к балансу)
    public static InlineKeyboardMarkup BalanceDetails()
    {
        return new InlineKeyboardMarkup(
            new[] { InlineKeyboardButton.WithCallbackData("🔙 Назад", "bal:back_to_dashboard") });
    }
}
