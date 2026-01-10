using Telegram.Bot.Types.ReplyMarkups;

namespace Console.Bot.Keyboards;

public static class GoalKeyboards
{
    // Карточка главной цели
    public static InlineKeyboardMarkup GoalMain() =>
        new(new[]
        {
            new[] 
            { 
                InlineKeyboardButton.WithCallbackData("➕ Пополнить", "goal:deposit"),
                InlineKeyboardButton.WithCallbackData("➖ Взять", "goal:withdraw")
            },
            new[] { InlineKeyboardButton.WithCallbackData("🔻 Сменить приоритет", "goal:list") },
            new[] 
            { 
                InlineKeyboardButton.WithCallbackData("⚙️ Настройки", "goal:settings"),
                InlineKeyboardButton.WithCallbackData("🔙 Назад", "menu:main")
            }
        });

    // Экран пополнения/снятия (быстрые суммы)
    public static InlineKeyboardMarkup GoalAmount(string prefix, decimal? freeBalance = null) =>
        new(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("100", $"{prefix}:100"),
                InlineKeyboardButton.WithCallbackData("500", $"{prefix}:500"),
                InlineKeyboardButton.WithCallbackData("1000", $"{prefix}:1000")
            },
            freeBalance.HasValue
                ? new[] { InlineKeyboardButton.WithCallbackData($"Все ({freeBalance:F0})", $"{prefix}:all") }
                : Array.Empty<InlineKeyboardButton>(),
            new[] { InlineKeyboardButton.WithCallbackData("🔙 Отмена", "goal:main") }
        });
    
    // Список целей для смены приоритета
    public static InlineKeyboardMarkup GoalList(IReadOnlyList<Domain.Entities.Goal> goals, int currentMainId)
    {
        var buttons = new List<InlineKeyboardButton[]>();
        foreach (var g in goals.Take(5))
        {
            var icon = g.Id == currentMainId ? "🎯" : "❄️";
            var percent = g.TargetAmount > 0 ? (g.CurrentAmount / g.TargetAmount) * 100 : 0;
            buttons.Add(new[] { InlineKeyboardButton.WithCallbackData(
                $"{icon} {g.Name} ({percent:F0}%)", 
                $"goal:select:{g.Id}") });
        }
        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("➕ Создать новую", "goal:create") });
        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("🔙 Назад", "goal:main") });
        return new InlineKeyboardMarkup(buttons);
    }
    
    // Диалог переноса денег
    public static InlineKeyboardMarkup GoalTransfer(string newGoalName, decimal amount) =>
        new(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData($"➡️ Перенести в {newGoalName}", "goal:transfer:yes") },
            new[] { InlineKeyboardButton.WithCallbackData("❄️ Оставить (заморозить)", "goal:transfer:no") },
            new[] { InlineKeyboardButton.WithCallbackData("🔙 Отмена", "goal:list") }
        });
    
    // Победа!
    public static InlineKeyboardMarkup GoalVictory(int goalId) =>
        new(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("🛍 Я купил это! (Списать)", $"goal:bought:{goalId}") },
            new[] { InlineKeyboardButton.WithCallbackData("👀 Продолжить копить", $"goal:raise:{goalId}") },
            new[] { InlineKeyboardButton.WithCallbackData("🔙 Не трогать пока", "goal:main") }
        });
    
    // Настройки цели
    public static InlineKeyboardMarkup GoalSettings(int goalId) =>
        new(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("✏️ Название", $"goal:edit:name:{goalId}"),
                InlineKeyboardButton.WithCallbackData("💵 Сумма", $"goal:edit:amount:{goalId}")
            },
            new[] { InlineKeyboardButton.WithCallbackData("🗑 Удалить цель", $"goal:delete:{goalId}") },
            new[] { InlineKeyboardButton.WithCallbackData("🔙 Назад", "goal:main") }
        });
    
    // Пустой экран (нет целей)
    public static InlineKeyboardMarkup GoalEmpty() =>
        new(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("➕ Создать первую цель", "goal:create") },
            new[] { InlineKeyboardButton.WithCallbackData("🔙 Назад", "menu:main") }
        });
    
    // Отмена ввода текста
    public static InlineKeyboardMarkup GoalCancel() =>
        new(new[] { new[] { InlineKeyboardButton.WithCallbackData("🔙 Отмена", "goal:main") } });
}
