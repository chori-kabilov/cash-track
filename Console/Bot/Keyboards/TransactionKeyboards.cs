using Domain.Entities;
using Domain.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Console.Bot.Keyboards;

public static class TransactionKeyboards
{
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
}
