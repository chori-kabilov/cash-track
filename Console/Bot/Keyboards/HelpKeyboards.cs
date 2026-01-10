using Telegram.Bot.Types.ReplyMarkups;

namespace Console.Bot.Keyboards;

// Клавиатуры для Помощи
public static class HelpKeyboards
{
    // Главное меню помощи
    public static InlineKeyboardMarkup Main() => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("📖 Как пользоваться", "help:guide") },
        new[] { InlineKeyboardButton.WithCallbackData("📱 Написать разработчику", "help:contact") },
        new[] 
        { 
            InlineKeyboardButton.WithCallbackData("🐛 Ошибка", "help:bug"),
            InlineKeyboardButton.WithCallbackData("💡 Идея", "help:idea")
        },
        new[] { InlineKeyboardButton.WithCallbackData("🔙 Главное меню", "menu:main") }
    });

    // Справочник функций
    public static InlineKeyboardMarkup Guide() => new(new[]
    {
        new[] 
        { 
            InlineKeyboardButton.WithCallbackData("💰 Баланс", "help:guide:balance"),
            InlineKeyboardButton.WithCallbackData("📊 Статистика", "help:guide:stats")
        },
        new[] 
        { 
            InlineKeyboardButton.WithCallbackData("🎯 Цели", "help:guide:goals"),
            InlineKeyboardButton.WithCallbackData("💸 Долги", "help:guide:debts")
        },
        new[] { InlineKeyboardButton.WithCallbackData("🔄 Платежи", "help:guide:regular") },
        new[] { InlineKeyboardButton.WithCallbackData("🔙 Назад", "help:main") }
    });

    // Назад в справочник
    public static InlineKeyboardMarkup BackToGuide() => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("🔙 Назад", "help:guide") }
    });

    // К функции + назад
    public static InlineKeyboardMarkup GuideWithAction(string actionLabel, string actionCallback) => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData(actionLabel, actionCallback) },
        new[] { InlineKeyboardButton.WithCallbackData("🔙 Назад", "help:guide") }
    });

    // Назад в помощь
    public static InlineKeyboardMarkup BackToHelp() => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("🔙 Назад", "help:main") }
    });

    // Отмена ввода
    public static InlineKeyboardMarkup Cancel() => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("❌ Отмена", "help:main") }
    });

    // После отправки отзыва
    public static InlineKeyboardMarkup AfterFeedback() => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("🔙 К помощи", "help:main") }
    });
}
