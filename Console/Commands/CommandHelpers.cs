using System.Text;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace Console.Commands;

// Базовый класс для всех команд — общие хелперы
public static class CommandHelpers
{
    // Построить прогресс-бар (используется в Goals, Debts, Limits)
    public static string BuildProgressBar(decimal current, decimal target, int width = 10)
    {
        if (target <= 0) return new string('░', width);
        var progress = Math.Min(1m, current / target);
        var filled = (int)(progress * width);
        return new string('▓', filled) + new string('░', width - filled);
    }

    // Экранировать Markdown спецсимволы
    public static string EscapeMarkdown(string text)
    {
        return text.Replace("_", "\\_").Replace("*", "\\*").Replace("`", "\\`").Replace("[", "\\[");
    }

    // Форматировать сумму с валютой
    public static string FormatAmount(decimal amount, string currency = "TJS")
    {
        return $"{amount:N0} {currency}";
    }

    // Форматировать дату
    public static string FormatDate(DateTimeOffset? date)
    {
        return date?.ToString("dd.MM.yyyy") ?? "—";
    }

    // Создать пагинацию (prev/counter/next)
    public static InlineKeyboardButton[] BuildPaginationRow(int current, int total, string callbackPrefix)
    {
        var prevCallback = current > 0 ? $"{callbackPrefix}:{current - 1}" : $"{callbackPrefix}:noop";
        var nextCallback = current < total - 1 ? $"{callbackPrefix}:{current + 1}" : $"{callbackPrefix}:noop";

        return new[]
        {
            InlineKeyboardButton.WithCallbackData(current > 0 ? "◀️" : "•", prevCallback),
            InlineKeyboardButton.WithCallbackData($"{current + 1}/{total}", $"{callbackPrefix}:noop"),
            InlineKeyboardButton.WithCallbackData(current < total - 1 ? "▶️" : "•", nextCallback)
        };
    }

    // Отправить или отредактировать сообщение (с обработкой "не изменено")
    public static async Task SendOrEditAsync(ITelegramBotClient bot, long chatId, int? msgId, 
        string text, InlineKeyboardMarkup? keyboard, CancellationToken ct, string? callbackQueryId = null)
    {
        if (msgId.HasValue)
        {
            try
            {
                await bot.EditMessageTextAsync(chatId, msgId.Value, text, ParseMode.Markdown, replyMarkup: keyboard, cancellationToken: ct);
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("message is not modified"))
                {
                    await HandleMessageNotModifiedAsync(bot, chatId, callbackQueryId, ct);
                    return;
                }
                throw;
            }
        }
        else
        {
            await bot.SendTextMessageAsync(chatId, text, ParseMode.Markdown, replyMarkup: keyboard, cancellationToken: ct);
        }
    }

    // Безопасное редактирование сообщения (для вызова напрямую)
    public static async Task SafeEditMessageAsync(ITelegramBotClient bot, long chatId, int msgId, 
        string text, InlineKeyboardMarkup? keyboard, CancellationToken ct, string? callbackQueryId = null)
    {
        try
        {
            await bot.EditMessageTextAsync(chatId, msgId, text, ParseMode.Markdown, replyMarkup: keyboard, cancellationToken: ct);
        }
        catch (Exception ex)
        {
             if (ex.Message.Contains("message is not modified"))
             {
                 await HandleMessageNotModifiedAsync(bot, chatId, callbackQueryId, ct);
                 return;
             }
             throw;
        }
    }

    // Логика "умного уведомления"
    private static async Task HandleMessageNotModifiedAsync(ITelegramBotClient bot, long chatId, string? callbackQueryId, CancellationToken ct)
    {
        // 1. Остановить спиннер
        if (callbackQueryId != null)
        {
            try { await bot.AnswerCallbackQueryAsync(callbackQueryId, cancellationToken: ct); } catch { }
        }

        // 2. Отправить самоликвидирующееся сообщение
        try 
        {
             var msg = await bot.SendTextMessageAsync(chatId, "⏳ Данные уже актуальны. Не нажимайте часто!", cancellationToken: ct);
             _ = Task.Run(async () => 
             {
                 try 
                 {
                     await Task.Delay(2000, CancellationToken.None);
                     await bot.DeleteMessageAsync(chatId, msg.MessageId, CancellationToken.None);
                 }
                 catch { }
             }, ct);
        }
        catch { }
    }

    // Получить эмодзи статуса (для процентов)
    public static string GetStatusEmoji(decimal percent)
    {
        return percent switch
        {
            >= 100 => "🔴",
            >= 80 => "⚠️",
            >= 50 => "🟡",
            _ => "✅"
        };
    }

    // Построить текстовый список с нумерацией
    public static string BuildNumberedList<T>(IEnumerable<T> items, Func<T, int, string> formatter)
    {
        var sb = new StringBuilder();
        var index = 1;
        foreach (var item in items)
        {
            sb.AppendLine($"{index}. {formatter(item, index)}");
            index++;
        }
        return sb.ToString();
    }
}
