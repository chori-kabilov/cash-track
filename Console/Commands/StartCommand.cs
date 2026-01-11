using Console.Bot;
using Infrastructure.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Console.Commands;

// Команда /start — приветствие и инициализация пользователя
public class StartCommand(IUserService userService, ICategoryService categoryService)
{
    // Выполнить команду старта
    public async Task ExecuteAsync(ITelegramBotClient bot, long chatId, User telegramUser, CancellationToken ct)
    {
        // Создаём/обновляем пользователя в БД
        var domainUser = MapToDomainUser(telegramUser);
        var user = await userService.CreateOrUpdateAsync(domainUser, ct);
        
        // Инициализируем категории по умолчанию
        await categoryService.InitializeDefaultCategoriesAsync(user.Id, ct);

        // Отправляем приветствие
        await SendWelcomeMessageAsync(bot, chatId, telegramUser.FirstName, ct);
    }

    private static Domain.Entities.User MapToDomainUser(User tgUser) => new()
    {
        Id = tgUser.Id,
        FirstName = tgUser.FirstName,
        LastName = tgUser.LastName,
        Username = tgUser.Username,
        LanguageCode = tgUser.LanguageCode,
        IsBot = tgUser.IsBot,
        LastMessageAt = DateTimeOffset.UtcNow
    };

    private static async Task SendWelcomeMessageAsync(ITelegramBotClient bot, long chatId, string? firstName, CancellationToken ct)
    {
        var text = $"👋 *Привет, {firstName ?? "друг"}!*\n\n" +
                   "Я — *CashTrack*, твой финансовый помощник. 🚀\n\n" +
                   "📌 *Что я умею:*\n" +
                   "• Записывать доходы и расходы\n" +
                   "• Ставить финансовые цели\n" +
                   "• Следить за долгами\n" +
                   "• Считать регулярные платежи\n\n" +
                   "👇 *Выберите действие:*";

        await bot.SendTextMessageAsync(chatId, text, ParseMode.Markdown, 
            replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
    }
}
