using Domain.Entities;
using Domain.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Console.Bot.Keyboards;

// Клавиатуры для транзакций (доход/расход)
public static class TransactionKeyboards
{
    /// <summary>
    /// Меню выбора категории (5 рядов):
    /// Страница 0: Ряд 1 = топ-2, ряды 2-3 = 4 категории
    /// Страницы 1+: Ряды 1-3 = 6 категорий (без топ-2)
    /// Ряд 4: Пагинация [◀] [➕ Новая] [▶]
    /// Ряд 5: [❌ Отмена] [🔙 Назад]
    /// </summary>
    public static InlineKeyboardMarkup SmartCategories(
        IReadOnlyList<Category> top2Categories,
        IReadOnlyList<Category> otherCategories,
        TransactionType type,
        int page = 0)
    {
        var buttons = new List<InlineKeyboardButton[]>();
        var typeCode = (int)type;
        
        // Размер страницы: 4 на первой (есть топ-2), 6 на остальных
        var pageSize = page == 0 ? 4 : 6;
        
        // Ряд 1: Топ-2 категории (только на первой странице)
        if (page == 0 && top2Categories.Count > 0)
        {
            buttons.Add(top2Categories.Take(2).Select(c =>
                InlineKeyboardButton.WithCallbackData($"{c.Icon} {Truncate(c.Name, 10)}", $"cat:{typeCode}:{c.Id}")).ToArray());
        }

        // Вычисляем offset с учётом разных размеров страниц
        // Страница 0: 4 категории, страницы 1+: 6 категорий
        var offset = page == 0 ? 0 : 4 + (page - 1) * 6;
        var pageItems = otherCategories.Skip(offset).Take(pageSize).ToList();
        
        // Ряды с категориями (2-3 на странице 0, 1-3 на остальных)
        foreach (var chunk in pageItems.Chunk(2))
        {
            buttons.Add(chunk.Select(c =>
                InlineKeyboardButton.WithCallbackData($"{c.Icon} {Truncate(c.Name, 10)}", $"cat:{typeCode}:{c.Id}")).ToArray());
        }

        // Подсчёт страниц: страница 0 = 4, остальные = 6
        var remainingAfterFirst = Math.Max(0, otherCategories.Count - 4);
        var totalPages = otherCategories.Count <= 4 ? 1 : 1 + (remainingAfterFirst + 5) / 6;
        
        // Ряд 4: Пагинация (кнопки показываются только когда активны)
        var navRow = new List<InlineKeyboardButton>();
        
        // Кнопка ◀ только если не на первой странице
        if (page > 0)
        {
            navRow.Add(InlineKeyboardButton.WithCallbackData("◀", $"cat:page:{page - 1}"));
        }
        
        navRow.Add(InlineKeyboardButton.WithCallbackData("➕ Новая", "cat:new"));
        
        // Кнопка ▶ только если есть следующая страница
        if (page < totalPages - 1)
        {
            navRow.Add(InlineKeyboardButton.WithCallbackData("▶", $"cat:page:{page + 1}"));
        }
        
        buttons.Add(navRow.ToArray());

        // Ряд 5: Отмена и Назад
        buttons.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData("❌ Отмена", "action:cancel:edit"),
            InlineKeyboardButton.WithCallbackData("🔙 Назад", "back:amount")
        });

        return new InlineKeyboardMarkup(buttons);
    }



    // Для расхода — начальный экран с эмоциями
    public static InlineKeyboardMarkup ExpenseStart(bool isImpulsive)
    {
        var toggleText = isImpulsive ? "🔥 На эмоцию" : "❄️ Обычно";
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("❌ Отмена", "action:cancel:edit"),
                InlineKeyboardButton.WithCallbackData(toggleText, "action:toggle_impulsive")
            }
        });
    }

    // Для дохода — начальный экран
    public static InlineKeyboardMarkup IncomeStart()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("❌ Отмена", "action:cancel:edit") }
        });
    }

    // Ввод названия новой категории
    public static InlineKeyboardMarkup NewCategoryInput()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("🔙 К выбору", "back:categories") }
        });
    }

    // Подтверждение транзакции
    public static InlineKeyboardMarkup TransactionConfirm()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("❌ Отменить", "txn:cancel"),
                InlineKeyboardButton.WithCallbackData("✅ Сохранить", "txn:done")
            }
        });
    }

    // После записи транзакции (2 кнопки)
    public static InlineKeyboardMarkup AfterTransaction()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🏠 Меню", "menu:main"),
                InlineKeyboardButton.WithCallbackData("💰 Баланс", "menu:balance")
            }
        });
    }

    // После отмены — просто главное меню
    public static InlineKeyboardMarkup AfterCancel()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("🏠 Главное меню", "menu:main") }
        });
    }

    // Вспомогательные методы

    private static string Truncate(string text, int maxLen)
    {
        return text.Length <= maxLen ? text : text[..(maxLen - 1)] + "…";
    }

    #region === LEGACY ===

    public static InlineKeyboardMarkup Categories(IReadOnlyList<Category> categories, TransactionType type)
        => SmartCategories(new List<Category>(), categories, type);

    public static InlineKeyboardMarkup CategoriesWithBack(IReadOnlyList<Category> categories, TransactionType type)
        => SmartCategories(new List<Category>(), categories, type);

    public static InlineKeyboardMarkup TransactionComplete() => TransactionConfirm();

    #endregion
}
