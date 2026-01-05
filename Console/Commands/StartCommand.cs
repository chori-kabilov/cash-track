using Console.Bot;
using Infrastructure.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

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
            text: "👋 *Привет! Я — CashTrack.*\n\n" +
                  "Я помогу тебе взять финансы под контроль. 🚀\n\n" +
                  "📌 *Что я умею:*\n" +
                  "▫️ Записывать доходы и расходы\n" +
                  "▫️ Ставить финансовые цели\n" +
                  "▫️ Следить за долгами\n" +
                  "▫️ Считать регулярные платежи\n\n" +
                  "👇 Нажми кнопку в меню, чтобы начать!",
            parseMode: ParseMode.Markdown,
            replyMarkup: new ReplyKeyboardRemove(),
            cancellationToken: cancellationToken);

        // Send Main Menu via helper or command? 
        // Better to reuse a shared helper or just send it here.
        // Copying SendMainMenuAsync logic here for independence.
        await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: "Выберите действие:",
            replyMarkup: BotInlineKeyboards.MainMenu(),
            cancellationToken: cancellationToken);
    }
}
