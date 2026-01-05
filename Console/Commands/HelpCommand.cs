using Console.Bot;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace Console.Commands;

public class HelpCommand
{
    public async Task ExecuteAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken, int? messageId = null)
    {
        var text = "ℹ️ *Справка CashTrack*\n\n" +
                   "Используйте *меню кнопок* для навигации. 📱\n\n" +
                   "➕ *Доход/Расход* — Запишите операцию.\n" +
                   "💰 *Баланс* — Узнайте сколько денег сейчас.\n" +
                   "📊 *Статистика* — Куда уходят деньги (топ категорий).\n" +
                   "🎯 *Цели* — Копите на мечту.\n" +
                   "🤝 *Долги* — Не забывайте, кто должен вам, и кому должны вы.\n" +
                   "🔄 *Платежи* — Контроль подписок и ЖКХ.\n\n" +
                   $"_{BotPersonality.GetRandomQuote()}_";

        if (messageId.HasValue)
        {
            await botClient.EditMessageTextAsync(chatId, messageId.Value, text, ParseMode.Markdown, replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: cancellationToken);
        }
        else
        {
            await botClient.SendTextMessageAsync(chatId, text, ParseMode.Markdown, replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: cancellationToken);
        }
    }
}
