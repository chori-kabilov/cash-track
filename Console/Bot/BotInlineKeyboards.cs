using Console.Bot.Keyboards;
using Domain.Entities;
using Domain.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Console.Bot;

// Фасад для всех клавиатур — сохраняет обратную совместимость
public static class BotInlineKeyboards
{
    // MainMenu
    public static InlineKeyboardMarkup MainMenu() => MainMenuKeyboards.MainMenu();
    public static InlineKeyboardMarkup Cancel() => MainMenuKeyboards.Cancel();

    // Transaction
    public static InlineKeyboardMarkup Categories(IReadOnlyList<Category> categories, TransactionType type) 
        => TransactionKeyboards.Categories(categories, type);
    public static InlineKeyboardMarkup ExpenseStart(bool isImpulsive) 
        => TransactionKeyboards.ExpenseStart(isImpulsive);
    public static InlineKeyboardMarkup CategoriesWithBack(IReadOnlyList<Category> categories, TransactionType type) 
        => TransactionKeyboards.CategoriesWithBack(categories, type);
    public static InlineKeyboardMarkup NewCategoryInput() 
        => TransactionKeyboards.NewCategoryInput();
    public static InlineKeyboardMarkup TransactionComplete() 
        => TransactionKeyboards.TransactionComplete();

    // Balance
    public static InlineKeyboardMarkup BalanceDashboard(bool showDebts, bool showGoals, bool showPayments) 
        => BalanceKeyboards.BalanceDashboard(showDebts, showGoals, showPayments);
    public static InlineKeyboardMarkup BalanceDetails() 
        => BalanceKeyboards.BalanceDetails();

    // Stat
    public static InlineKeyboardMarkup StatsSummary(string periodLabel) 
        => StatKeyboards.StatsSummary(periodLabel);
    public static InlineKeyboardMarkup StatsCategories(bool showExpenses) 
        => StatKeyboards.StatsCategories(showExpenses);
    public static InlineKeyboardMarkup StatsHistory(int page, int totalPages) 
        => StatKeyboards.StatsHistory(page, totalPages);
    public static InlineKeyboardMarkup StatsEmotions() 
        => StatKeyboards.StatsEmotions();
    public static InlineKeyboardMarkup StatsRegular() 
        => StatKeyboards.StatsRegular();
    public static InlineKeyboardMarkup StatsPeriodSelect() 
        => StatKeyboards.StatsPeriodSelect();

    // Goal
    public static InlineKeyboardMarkup GoalMain() 
        => GoalKeyboards.MainKeyboard();
    public static InlineKeyboardMarkup GoalEmpty() 
        => GoalKeyboards.Empty();
    public static InlineKeyboardMarkup GoalCancel() 
        => GoalKeyboards.Cancel();
}
