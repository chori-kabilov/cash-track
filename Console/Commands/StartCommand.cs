using Console.Bot;
using Infrastructure.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Console.Commands;

public class StartCommand(IUserService userService, ICategoryService categoryService)
{
    public async Task ExecuteAsync(ITelegramBotClient botClient, long chatId, User telegramUser, CancellationToken cancellationToken)
    {
        var domainUser = new Domain.Entities.User
        {
            Id = telegramUser.Id,
            FirstName = telegramUser.FirstName,
            LastName = telegramUser.LastName,
            Username = telegramUser.Username,
            LanguageCode = telegramUser.LanguageCode,
            IsBot = telegramUser.IsBot,
            LastMessageAt = DateTimeOffset.UtcNow
        };

        var user = await userService.CreateOrUpdateAsync(domainUser, cancellationToken);
        await categoryService.InitializeDefaultCategoriesAsync(user.Id, cancellationToken);

        await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: $"👋 *Привет, {telegramUser.FirstName}!*\n\n" +
                  "Я — *CashTrack*, твой финансовый помощник. 🚀\n\n" +
                  "📌 *Что я умею:*\n" +
                  "• Записывать доходы и расходы\n" +
                  "• Ставить финансовые цели\n" +
                  "• Следить за долгами\n" +
                  "• Считать регулярные платежи\n\n" +
                  "👇 *Выберите действие:*",
            parseMode: ParseMode.Markdown,
            replyMarkup: BotInlineKeyboards.MainMenu(),
            cancellationToken: cancellationToken);
    }
}
