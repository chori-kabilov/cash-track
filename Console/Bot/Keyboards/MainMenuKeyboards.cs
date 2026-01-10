using Telegram.Bot.Types.ReplyMarkups;

namespace Console.Bot.Keyboards;

public static class MainMenuKeyboards
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
                    InlineKeyboardButton.WithCallbackData("💸 Долги", "menu:debts")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("🔄 Платежи", "menu:regular"),
                    InlineKeyboardButton.WithCallbackData("ℹ️ Помощь", "menu:help")
                }
            });
    }

    public static InlineKeyboardMarkup Cancel()
    {
        return new InlineKeyboardMarkup(
            InlineKeyboardButton.WithCallbackData("Отмена", "action:cancel"));
    }
}
