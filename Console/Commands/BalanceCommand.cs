using Console.Bot;
using Infrastructure.Services;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace Console.Commands;

public class BalanceCommand(IAccountService accountService)
{
    public async Task ExecuteAsync(ITelegramBotClient botClient, long chatId, long userId, CancellationToken cancellationToken, int? messageId = null)
    {
        var account = await accountService.GetUserAccountAsync(userId, cancellationToken)
                      ?? await accountService.CreateAccountAsync(userId, ct: cancellationToken);

        var quote = BotPersonality.GetRandomQuote();
        var text = $"💰 *Ваш Баланс*\n\n" +
                   $"💳 Счёт: *{account.Name}*\n" +
                   $"💵 Сумма: *{account.Balance:F2} {account.Currency}*\n\n" +
                   $"_{quote}_";

        if (messageId.HasValue)
        {
            await botClient.EditMessageTextAsync(
                chatId: chatId,
                messageId: messageId.Value,
                text: text,
                parseMode: ParseMode.Markdown,
                replyMarkup: BotInlineKeyboards.MainMenu(),
                cancellationToken: cancellationToken);
        }
        else
        {
            await botClient.SendTextMessageAsync(
                chatId: chatId,
                text: text,
                parseMode: ParseMode.Markdown,
                replyMarkup: BotInlineKeyboards.MainMenu(),
                cancellationToken: cancellationToken);
        }
    }
}
