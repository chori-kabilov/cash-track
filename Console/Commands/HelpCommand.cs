using System.Text;
using Console.Bot.Keyboards;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace Console.Commands;

// Команда для Помощи (справка + обратная связь)
public class HelpCommand
{
    private readonly long? _feedbackChatId;
    private const string DeveloperUsername = "@kabilov_chori";

    public HelpCommand(long? feedbackChatId = null)
    {
        _feedbackChatId = feedbackChatId;
    }

    // Точка входа
    public async Task ExecuteAsync(ITelegramBotClient bot, long chatId, CancellationToken ct, int? msgId = null)
    {
        if (msgId.HasValue)
            await ShowMainAsync(bot, chatId, msgId.Value, ct);
        else
        {
            var msg = await bot.SendTextMessageAsync(chatId, "ℹ️ Загрузка...", cancellationToken: ct);
            await ShowMainAsync(bot, chatId, msg.MessageId, ct);
        }
    }

    // === ЭКРАНЫ ===

    // Главное меню помощи
    public async Task ShowMainAsync(ITelegramBotClient bot, long chatId, int msgId, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ℹ️ *CashTrack — Помощь*\n");
        sb.AppendLine("Твой личный финансовый помощник 💰\n");
        sb.AppendLine("*Что умеет бот:*");
        sb.AppendLine("• ➕➖ Записывать доходы и расходы");
        sb.AppendLine("• 📊 Показывать статистику");
        sb.AppendLine("• 🎯 Копить на цели");
        sb.AppendLine("• 💸 Отслеживать долги");
        sb.AppendLine("• 🔄 Напоминать о платежах");
        sb.AppendLine("\nДля деталей выберите раздел ⬇️");

        await bot.EditMessageTextAsync(chatId, msgId, sb.ToString(),
            ParseMode.Markdown, replyMarkup: HelpKeyboards.Main(), cancellationToken: ct);
    }

    // Справочник
    public async Task ShowGuideAsync(ITelegramBotClient bot, long chatId, int msgId, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("📖 *Справочник*\n");
        sb.AppendLine("Выберите функцию для подробностей:");

        await bot.EditMessageTextAsync(chatId, msgId, sb.ToString(),
            ParseMode.Markdown, replyMarkup: HelpKeyboards.Guide(), cancellationToken: ct);
    }

    // Справка: Баланс
    public async Task ShowGuideBalanceAsync(ITelegramBotClient bot, long chatId, int msgId, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("💰 *Баланс*\n");
        sb.AppendLine("Показывает вашу финансовую картину:\n");
        sb.AppendLine("*Что видно:*");
        sb.AppendLine("• Текущий баланс");
        sb.AppendLine("• Прогресс по цели");
        sb.AppendLine("• Ближайшие платежи");
        sb.AppendLine("• Итог долгов\n");
        sb.AppendLine("*Формула:*");
        sb.AppendLine("Баланс = Доходы − Расходы\n");
        sb.AppendLine("💡 _Старайтесь не уходить в минус!_");

        await bot.EditMessageTextAsync(chatId, msgId, sb.ToString(),
            ParseMode.Markdown, replyMarkup: HelpKeyboards.GuideWithAction("💰 К Балансу", "menu:balance"), cancellationToken: ct);
    }

    // Справка: Статистика
    public async Task ShowGuideStatsAsync(ITelegramBotClient bot, long chatId, int msgId, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("📊 *Статистика*\n");
        sb.AppendLine("Анализ ваших трат и доходов.\n");
        sb.AppendLine("*Что видно:*");
        sb.AppendLine("• Расходы/доходы за период");
        sb.AppendLine("• Топ категорий");
        sb.AppendLine("• История транзакций\n");
        sb.AppendLine("*Периоды:*");
        sb.AppendLine("Неделя | Месяц | Год\n");
        sb.AppendLine("💡 _Следите за топ-3 категориями!_");

        await bot.EditMessageTextAsync(chatId, msgId, sb.ToString(),
            ParseMode.Markdown, replyMarkup: HelpKeyboards.GuideWithAction("📊 К Статистике", "menu:stats"), cancellationToken: ct);
    }

    // Справка: Цели
    public async Task ShowGuideGoalsAsync(ITelegramBotClient bot, long chatId, int msgId, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("🎯 *Цели накопления*\n");
        sb.AppendLine("Копите на мечту!\n");
        sb.AppendLine("*Как работает:*");
        sb.AppendLine("1. Создайте цель (название + сумма)");
        sb.AppendLine("2. Откладывайте с баланса");
        sb.AppendLine("3. Достигните цели!\n");
        sb.AppendLine("*Особенности:*");
        sb.AppendLine("• Одна активная цель");
        sb.AppendLine("• Остальные в очереди");
        sb.AppendLine("• Прогресс-бар и %");

        await bot.EditMessageTextAsync(chatId, msgId, sb.ToString(),
            ParseMode.Markdown, replyMarkup: HelpKeyboards.GuideWithAction("🎯 К Целям", "menu:goals"), cancellationToken: ct);
    }

    // Справка: Долги
    public async Task ShowGuideDebtsAsync(ITelegramBotClient bot, long chatId, int msgId, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("💸 *Долги*\n");
        sb.AppendLine("Отслеживайте кто кому должен.\n");
        sb.AppendLine("*Типы:*");
        sb.AppendLine("• 📥 Мне должны");
        sb.AppendLine("• 📤 Я должен\n");
        sb.AppendLine("*Возможности:*");
        sb.AppendLine("• Частичные платежи");
        sb.AppendLine("• История погашения");
        sb.AppendLine("• Связь с балансом");

        await bot.EditMessageTextAsync(chatId, msgId, sb.ToString(),
            ParseMode.Markdown, replyMarkup: HelpKeyboards.GuideWithAction("💸 К Долгам", "menu:debts"), cancellationToken: ct);
    }

    // Справка: Платежи
    public async Task ShowGuideRegularAsync(ITelegramBotClient bot, long chatId, int msgId, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("🔄 *Регулярные платежи*\n");
        sb.AppendLine("Не забывайте об обязательных тратах!\n");
        sb.AppendLine("*Примеры:*");
        sb.AppendLine("• Интернет, Аренда, Подписки\n");
        sb.AppendLine("*Как работает:*");
        sb.AppendLine("1. Добавьте платёж");
        sb.AppendLine("2. Укажите дату и сумму");
        sb.AppendLine("3. Отмечайте «Оплачено»\n");
        sb.AppendLine("💡 _При оплате — автосписание с баланса_");

        await bot.EditMessageTextAsync(chatId, msgId, sb.ToString(),
            ParseMode.Markdown, replyMarkup: HelpKeyboards.GuideWithAction("🔄 К Платежам", "menu:regular"), cancellationToken: ct);
    }

    // Контакт разработчика
    public async Task ShowContactAsync(ITelegramBotClient bot, long chatId, int msgId, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("📱 *Связь с разработчиком*\n");
        sb.AppendLine("Привет! Я разработчик CashTrack 👋\n");
        sb.AppendLine("Если есть вопросы, предложения");
        sb.AppendLine("или просто хотите поговорить:\n");
        sb.AppendLine($"👤 Telegram: `{DeveloperUsername}`\n");
        sb.AppendLine("💬 Напишите — отвечу!");

        await bot.EditMessageTextAsync(chatId, msgId, sb.ToString(),
            ParseMode.Markdown, replyMarkup: HelpKeyboards.BackToHelp(), cancellationToken: ct);
    }

    // Промпт для бага
    public async Task PromptBugReportAsync(ITelegramBotClient bot, long chatId, int msgId, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("🐛 *Сообщить об ошибке*\n");
        sb.AppendLine("Что-то не работает?");
        sb.AppendLine("Опишите проблему:\n");
        sb.AppendLine("_Пример: «При нажатии X ничего не происходит»_");

        await bot.EditMessageTextAsync(chatId, msgId, sb.ToString(),
            ParseMode.Markdown, replyMarkup: HelpKeyboards.Cancel(), cancellationToken: ct);
    }

    // Промпт для идеи
    public async Task PromptIdeaAsync(ITelegramBotClient bot, long chatId, int msgId, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("💡 *Предложить идею*\n");
        sb.AppendLine("Какой функции не хватает?");
        sb.AppendLine("Напишите вашу идею:\n");
        sb.AppendLine("_Пример: «Хочу видеть графики расходов»_");

        await bot.EditMessageTextAsync(chatId, msgId, sb.ToString(),
            ParseMode.Markdown, replyMarkup: HelpKeyboards.Cancel(), cancellationToken: ct);
    }

    // Отправить фидбек в канал/группу с возможностью ответа
    public async Task SendFeedbackAsync(ITelegramBotClient bot, long chatId, long userId, 
        string? firstName, string? lastName, string? username, string text, string type, CancellationToken ct)
    {
        var typeLabel = type == "bug" ? "🐛 БАГ" : "💡 ИДЕЯ";
        
        // Формируем имя пользователя
        var displayName = BuildDisplayName(firstName, lastName, username);
        var profileLink = username != null ? $"@{username}" : $"tg://user?id={userId}";
        
        var sb = new StringBuilder();
        sb.AppendLine($"{typeLabel}\n");
        sb.AppendLine($"👤 *Пользователь:* [{displayName}]({profileLink})");
        sb.AppendLine($"🆔 ID: `{userId}`");
        sb.AppendLine($"\n📝 *Сообщение:*\n{EscapeMarkdown(text)}");
        sb.AppendLine("\n━━━━━━━━━━━━");
        sb.AppendLine("💬 _Ответьте на это сообщение, чтобы связаться с пользователем_");

        // Отправка в канал/группу разработчика
        if (_feedbackChatId.HasValue)
        {
            try
            {
                await bot.SendTextMessageAsync(_feedbackChatId.Value, sb.ToString(),
                    ParseMode.Markdown, cancellationToken: ct);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Ошибка отправки фидбека: {ex.Message}");
            }
        }

        // Ответ пользователю
        var thanks = new StringBuilder();
        thanks.AppendLine(type == "bug" ? "✅ *Спасибо за обратную связь!*" : "✅ *Спасибо за идею!*");
        thanks.AppendLine("\nВаше сообщение отправлено разработчику.");
        thanks.AppendLine(type == "bug" ? "Постараюсь исправить!" : "Рассмотрю для будущих версий!");
        thanks.AppendLine($"\n📝 Ваш отзыв:\n«{text}»");

        await bot.SendTextMessageAsync(chatId, thanks.ToString(),
            ParseMode.Markdown, replyMarkup: HelpKeyboards.AfterFeedback(), cancellationToken: ct);
    }

    // Переслать ответ разработчика пользователю
    public async Task ForwardReplyToUserAsync(ITelegramBotClient bot, long adminChatId, string replyText, 
        long targetUserId, CancellationToken ct)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("💬 *Ответ от разработчика:*\n");
            sb.AppendLine(replyText);

            await bot.SendTextMessageAsync(targetUserId, sb.ToString(),
                ParseMode.Markdown, cancellationToken: ct);
            
            // Подтверждение в группу
            await bot.SendTextMessageAsync(adminChatId, 
                $"✅ Сообщение доставлено пользователю `{targetUserId}`",
                ParseMode.Markdown, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            await bot.SendTextMessageAsync(adminChatId, 
                $"❌ Не удалось доставить: {ex.Message}",
                cancellationToken: ct);
        }
    }

    // Извлечь userId из сообщения фидбека
    public static long? ExtractUserIdFromFeedback(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        
        // Ищем паттерн "ID: `12345`" или "ID: 12345"
        var match = System.Text.RegularExpressions.Regex.Match(text, @"ID:\s*`?(\d+)`?");
        if (match.Success && long.TryParse(match.Groups[1].Value, out var userId))
            return userId;
        
        return null;
    }

    // Хелперы
    private static string BuildDisplayName(string? firstName, string? lastName, string? username)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(firstName)) parts.Add(firstName);
        if (!string.IsNullOrWhiteSpace(lastName)) parts.Add(lastName);
        
        if (parts.Count > 0)
        {
            var name = string.Join(" ", parts);
            return !string.IsNullOrWhiteSpace(username) ? $"{name} (@{username})" : name;
        }
        
        return !string.IsNullOrWhiteSpace(username) ? $"@{username}" : "Аноним";
    }

    private static string EscapeMarkdown(string text)
    {
        // Экранируем специальные символы Markdown
        return text.Replace("_", "\\_").Replace("*", "\\*").Replace("`", "\\`").Replace("[", "\\[");
    }
}
