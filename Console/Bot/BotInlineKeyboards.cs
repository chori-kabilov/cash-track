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

    // Для расходов — с опцией импульсивной покупки
    public static InlineKeyboardMarkup SkipDescription(bool isImpulsive)
    {
        var impulsiveText = isImpulsive ? "✅ На эмоциях" : "🛍️ На эмоциях";
        return new InlineKeyboardMarkup(
            new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("Пропустить", "action:skip_desc"),
                    InlineKeyboardButton.WithCallbackData(impulsiveText, "action:toggle_impulsive")
                },
                new[] { InlineKeyboardButton.WithCallbackData("Отмена", "action:cancel") }
            });
    }

    // Для дохода — после записи предложить добавить описание
    public static InlineKeyboardMarkup IncomeComplete(bool hasDescription)
    {
        if (hasDescription)
        {
            return new InlineKeyboardMarkup(
                new[] { InlineKeyboardButton.WithCallbackData("✅ Готово", "income:done") });
        }
        
        return new InlineKeyboardMarkup(
            new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("📝 Описание", "income:add_desc"),
                    InlineKeyboardButton.WithCallbackData("✅ Готово", "income:done")
                }
            });
    }

    // Ввод описания для дохода с кнопкой "Назад"
    public static InlineKeyboardMarkup IncomeDescription()
    {
        return new InlineKeyboardMarkup(
            new[] { InlineKeyboardButton.WithCallbackData("🔙 Назад", "income:back") });
    }
}
