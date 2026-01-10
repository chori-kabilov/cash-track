using Telegram.Bot.Types.ReplyMarkups;

namespace Console.Bot.Keyboards;

public static class StatKeyboards
{
    // Главный экран: Сводка
    public static InlineKeyboardMarkup StatsSummary(string periodLabel)
    {
        return new InlineKeyboardMarkup(
            new[]
            {
                new[] // Ряд 1: Навигация по периоду
                { 
                    InlineKeyboardButton.WithCallbackData("<", "stat:prev"),
                    InlineKeyboardButton.WithCallbackData($"📅 {periodLabel}", "stat:period"),
                    InlineKeyboardButton.WithCallbackData(">", "stat:next")
                },
                new[] // Ряд 2: Основные
                { 
                    InlineKeyboardButton.WithCallbackData("📂 Категории", "stat:categories"),
                    InlineKeyboardButton.WithCallbackData("📜 История", "stat:history")
                },
                new[] // Ряд 3: Аналитика
                { 
                    InlineKeyboardButton.WithCallbackData("🌪 Эмоции", "stat:emotions"),
                    InlineKeyboardButton.WithCallbackData("📅 Регулярные", "stat:regular")
                },
                new[] // Ряд 4: Действия
                { 
                    InlineKeyboardButton.WithCallbackData("📄 Excel", "stat:export"),
                    InlineKeyboardButton.WithCallbackData("🔙 Меню", "stat:back")
                }
            });
    }

    // Категории (с переключателем Расходы/Доходы)
    public static InlineKeyboardMarkup StatsCategories(bool showExpenses)
    {
        var expBtn = showExpenses 
            ? InlineKeyboardButton.WithCallbackData("🔵 Расходы", "stat:cat:exp")
            : InlineKeyboardButton.WithCallbackData("⚪️ Расходы", "stat:cat:exp");
        var incBtn = showExpenses 
            ? InlineKeyboardButton.WithCallbackData("⚪️ Доходы", "stat:cat:inc")
            : InlineKeyboardButton.WithCallbackData("🔵 Доходы", "stat:cat:inc");

        return new InlineKeyboardMarkup(
            new[]
            {
                new[] { expBtn, incBtn },
                new[] { InlineKeyboardButton.WithCallbackData("🔙 Назад", "stat:summary") }
            });
    }

    // История (с пагинацией)
    public static InlineKeyboardMarkup StatsHistory(int page, int totalPages)
    {
        return new InlineKeyboardMarkup(
            new[]
            {
                new[]
                { 
                    InlineKeyboardButton.WithCallbackData("<", "stat:page:prev"),
                    InlineKeyboardButton.WithCallbackData($"{page}/{totalPages}", "stat:noop"),
                    InlineKeyboardButton.WithCallbackData(">", "stat:page:next")
                },
                new[] { InlineKeyboardButton.WithCallbackData("🔙 Назад", "stat:summary") }
            });
    }

    // Эмоции (просто кнопка назад)
    public static InlineKeyboardMarkup StatsEmotions()
    {
        return new InlineKeyboardMarkup(
            new[] { InlineKeyboardButton.WithCallbackData("🔙 Назад", "stat:summary") });
    }

    // Регулярные (просто кнопка назад)
    public static InlineKeyboardMarkup StatsRegular()
    {
        return new InlineKeyboardMarkup(
            new[] { InlineKeyboardButton.WithCallbackData("🔙 Назад", "stat:summary") });
    }

    // Выбор периода (Неделя/Месяц/Год)
    public static InlineKeyboardMarkup StatsPeriodSelect()
    {
        return new InlineKeyboardMarkup(
            new[]
            {
                new[]
                { 
                    InlineKeyboardButton.WithCallbackData("Неделя", "stat:period:week"),
                    InlineKeyboardButton.WithCallbackData("Месяц", "stat:period:month"),
                    InlineKeyboardButton.WithCallbackData("Год", "stat:period:year")
                },
                new[] { InlineKeyboardButton.WithCallbackData("🔙 Отмена", "stat:summary") }
            });
    }
}
