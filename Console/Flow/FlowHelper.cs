using System.Globalization;
using Domain.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Console.Flow;

// Общие утилиты для Flow handlers
public static class FlowHelper
{
    // Парсинг суммы (поддержка запятой и точки)
    public static bool TryParseAmount(string text, out decimal amount) =>
        decimal.TryParse(text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out amount);

    // Клавиатура выбора периодичности
    public static InlineKeyboardMarkup FrequencyKeyboard() => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("Ежедневно", "reg:freq:Daily"), InlineKeyboardButton.WithCallbackData("Еженедельно", "reg:freq:Weekly") },
        new[] { InlineKeyboardButton.WithCallbackData("Ежемесячно", "reg:freq:Monthly"), InlineKeyboardButton.WithCallbackData("Ежегодно", "reg:freq:Yearly") }
    });

    // Escape для MarkdownV2
    public static string EscapeMd(string text) => 
        text.Replace("_", "\\_").Replace("*", "\\*").Replace("[", "\\[").Replace("]", "\\]")
            .Replace("(", "\\(").Replace(")", "\\)").Replace("~", "\\~").Replace("`", "\\`")
            .Replace(">", "\\>").Replace("#", "\\#").Replace("+", "\\+").Replace("-", "\\-")
            .Replace("=", "\\=").Replace("|", "\\|").Replace("{", "\\{").Replace("}", "\\}")
            .Replace(".", "\\.").Replace("!", "\\!");
}
