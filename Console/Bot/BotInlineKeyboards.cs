using Domain.Entities;
using Domain.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Console.Bot;

public static class BotInlineKeyboards
{
    public static InlineKeyboardMarkup MainMenu()
    {
        return new InlineKeyboardMarkup(
            new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("➕ Доход", "menu:income"),
                    InlineKeyboardButton.WithCallbackData("➖ Расход", "menu:expense")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("💰 Баланс", "menu:balance"),
                    InlineKeyboardButton.WithCallbackData("📊 Статистика", "menu:stats")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🎯 Цели", "menu:goals"),
                    InlineKeyboardButton.WithCallbackData("🤝 Долги", "menu:debts")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("📉 Лимиты", "menu:limits"),
                    InlineKeyboardButton.WithCallbackData("🔄 Платежи", "menu:regular")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("ℹ️ Помощь", "menu:help")
                }
            });
    }

    public static InlineKeyboardMarkup Cancel()
    {
        return new InlineKeyboardMarkup(
            InlineKeyboardButton.WithCallbackData("Отмена", "action:cancel"));
    }

    public static InlineKeyboardMarkup Categories(IReadOnlyList<Category> categories, TransactionType type)
    {
        var buttons = categories
            .Select(c => InlineKeyboardButton.WithCallbackData($"{c.Icon} {c.Name}".Trim(), $"cat:{(int)type}:{c.Id}"))
            .Chunk(2)
            .Select(row => row.ToArray())
            .ToList();

        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("📥 Другое", "cat:new") });
        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("Отмена", "action:cancel") });

        return new InlineKeyboardMarkup(buttons);
    }

    // Для расхода — первый экран с кнопкой "На эмоциях"
    public static InlineKeyboardMarkup ExpenseStart(bool isImpulsive)
    {
        var impulsiveText = isImpulsive ? "✅ На эмоциях" : "🌪 На эмоциях: ВЫКЛ";
        return new InlineKeyboardMarkup(
            new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData(impulsiveText, "action:toggle_impulsive") },
                new[] { InlineKeyboardButton.WithCallbackData("❌ Отмена", "action:cancel") }
            });
    }

    // Категории с кнопкой "Назад"
    public static InlineKeyboardMarkup CategoriesWithBack(IReadOnlyList<Category> categories, TransactionType type)
    {
        var buttons = categories
            .Select(c => InlineKeyboardButton.WithCallbackData($"{c.Icon} {c.Name}".Trim(), $"cat:{(int)type}:{c.Id}"))
            .Chunk(2)
            .Select(row => row.ToArray())
            .ToList();

        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("📥 Другое", "cat:new") });
        buttons.Add(new[] 
        { 
            InlineKeyboardButton.WithCallbackData("🔙 Назад", "back:amount"),
            InlineKeyboardButton.WithCallbackData("❌ Отмена", "action:cancel") 
        });

        return new InlineKeyboardMarkup(buttons);
    }

    // Ввод названия новой категории
    public static InlineKeyboardMarkup NewCategoryInput()
    {
        return new InlineKeyboardMarkup(
            new[] { InlineKeyboardButton.WithCallbackData("🔙 Назад", "back:categories") });
    }

    // Итоговое сообщение с кнопками "Готово" и "Отменить"
    public static InlineKeyboardMarkup TransactionComplete()
    {
        return new InlineKeyboardMarkup(
            new[]
            {
                InlineKeyboardButton.WithCallbackData("✅ Готово", "txn:done"),
                InlineKeyboardButton.WithCallbackData("❌ Отменить", "txn:cancel")
            });
    }

    // Баланс — панель с переключателями
    public static InlineKeyboardMarkup BalanceDashboard(bool showDebts, bool showGoals, bool showPayments)
    {
        var debtsText = showDebts ? "🟢 Долги" : "🔴 Долги: ВЫКЛ";
        var goalsText = showGoals ? "🟢 Цели" : "⚪️ Цели: ВЫКЛ";
        var paymentsText = showPayments ? "🟢 Платежи" : "⚪️ Платежи: ВЫКЛ";

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
                    InlineKeyboardButton.WithCallbackData("📊 Детали", "bal:details")
                }
            });
    }

    // Баланс — деталі (только кнопка назад к балансу)
    public static InlineKeyboardMarkup BalanceDetails()
    {
        return new InlineKeyboardMarkup(
            new[] { InlineKeyboardButton.WithCallbackData("🔙 Назад", "bal:back_to_dashboard") });
    }

    // ========== СТАТИСТИКА ==========

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
