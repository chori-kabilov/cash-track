using System.Text;
using Console.Bot;
using Console.Flow;
using Domain.Enums;
using Infrastructure.Services;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace Console.Commands;

// Команда для управления целями (Smart Goals Hub)
public class GoalCommand(IGoalService goalService, IAccountService accountService)
{
    // Точка входа: показывает главную цель или пустой экран
    public async Task ExecuteAsync(ITelegramBotClient bot, long chatId, long userId, UserFlowState flow, CancellationToken ct, int? messageId = null)
    {
        flow.CurrentGoalScreen = GoalScreen.Main;
        await RenderCurrentScreenAsync(bot, chatId, userId, flow, ct, messageId);
    }

    // Роутер экранов
    public async Task RenderCurrentScreenAsync(ITelegramBotClient bot, long chatId, long userId, UserFlowState flow, CancellationToken ct, int? messageId = null)
    {
        var (text, keyboard) = flow.CurrentGoalScreen switch
        {
            GoalScreen.Main => await BuildMainAsync(userId, ct),
            GoalScreen.List => await BuildListAsync(userId, ct),
            GoalScreen.Transfer => BuildTransfer(flow),
            GoalScreen.Deposit => await BuildDepositAsync(userId, ct),
            GoalScreen.Withdraw => await BuildWithdrawAsync(userId, ct),
            GoalScreen.Victory => await BuildVictoryAsync(userId, ct),
            GoalScreen.Settings => await BuildSettingsAsync(userId, ct),
            _ => ("🎯 Цели", BotInlineKeyboards.GoalEmpty())
        };

        if (messageId.HasValue)
            await bot.EditMessageTextAsync(chatId, messageId.Value, text, ParseMode.Markdown, replyMarkup: keyboard, cancellationToken: ct);
        else
            await bot.SendTextMessageAsync(chatId, text, ParseMode.Markdown, replyMarkup: keyboard, cancellationToken: ct);
    }

    // Сцена 1: Карточка главной цели
    private async Task<(string, Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup)> BuildMainAsync(long userId, CancellationToken ct)
    {
        var goals = await goalService.GetUserGoalsAsync(userId, ct);
        if (!goals.Any())
            return ("🎯 *Копилка пуста*\n\nУ вас пока нет финансовых целей.\nСоздайте первую, чтобы я помогал вам копить!", BotInlineKeyboards.GoalEmpty());

        var main = goals.OrderBy(g => g.Priority).FirstOrDefault(g => g.IsActive);
        if (main == null)
            return ("🎯 *Нет активной цели*\n\nВсе цели завершены или на паузе.", BotInlineKeyboards.GoalEmpty());

        var sb = new StringBuilder();
        var percent = main.TargetAmount > 0 ? (main.CurrentAmount / main.TargetAmount) * 100 : 0;
        var remaining = main.TargetAmount - main.CurrentAmount;

        sb.AppendLine($"🎯 *{main.Name}* (Главная цель)\n");
        sb.AppendLine($"💰 *Накоплено:* {main.CurrentAmount:N0} TJS");
        sb.AppendLine($"🏁 *Цель:* {main.TargetAmount:N0} TJS");
        sb.AppendLine($"📊 *Прогресс:* {BuildProgressBar(percent)} {percent:F0}%");
        sb.AppendLine($"⏳ *Осталось:* {remaining:N0} TJS");

        // Прогноз
        if (main.Deadline.HasValue)
        {
            var daysLeft = (main.Deadline.Value - DateTimeOffset.UtcNow).Days;
            sb.AppendLine($"\n📅 *Дедлайн:* {main.Deadline:dd.MM.yyyy} (через {daysLeft} дн.)");
            if (daysLeft > 0 && remaining > 0)
            {
                var perDay = remaining / daysLeft;
                sb.AppendLine($"💡 Откладывай по *{perDay:N0}* в день, чтобы успеть!");
            }
        }

        // Проверка на победу
        if (main.CurrentAmount >= main.TargetAmount)
            return BuildVictoryText(main);

        return (sb.ToString(), BotInlineKeyboards.GoalMain());
    }

    // Сцена 2: Экран пополнения
    private async Task<(string, Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup)> BuildDepositAsync(long userId, CancellationToken ct)
    {
        var main = await goalService.GetActiveGoalAsync(userId, ct);
        var account = await accountService.GetUserAccountAsync(userId, ct);
        var freeBalance = account?.Balance ?? 0;

        var sb = new StringBuilder();
        sb.AppendLine($"💳 *Пополнение \"{main?.Name ?? "Цель"}\"*\n");
        sb.AppendLine($"Свободно: *{freeBalance:N0} TJS*");
        sb.AppendLine($"В копилке: *{main?.CurrentAmount:N0} TJS*");
        sb.AppendLine("\n👇 Выберите сумму или введите свою:");

        return (sb.ToString(), BotInlineKeyboards.GoalAmount("goal:add", freeBalance));
    }

    // Сцена 2b: Экран снятия
    private async Task<(string, Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup)> BuildWithdrawAsync(long userId, CancellationToken ct)
    {
        var main = await goalService.GetActiveGoalAsync(userId, ct);

        var sb = new StringBuilder();
        sb.AppendLine($"💸 *Снятие из \"{main?.Name ?? "Цель"}\"*\n");
        sb.AppendLine($"В копилке: *{main?.CurrentAmount:N0} TJS*");
        sb.AppendLine("\n⚠️ Это отодвинет дату покупки.");
        sb.AppendLine("👇 Сколько снять?");

        return (sb.ToString(), BotInlineKeyboards.GoalAmount("goal:take", main?.CurrentAmount));
    }

    // Сцена 3: Список целей (смена приоритета)
    private async Task<(string, Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup)> BuildListAsync(long userId, CancellationToken ct)
    {
        var goals = await goalService.GetUserGoalsAsync(userId, ct);
        var main = goals.FirstOrDefault(g => g.IsActive && g.Priority == 1);

        var sb = new StringBuilder();
        sb.AppendLine("📋 *Ваши цели:*\n");

        foreach (var g in goals.OrderBy(x => x.Priority).Take(5))
        {
            var icon = g.Id == main?.Id ? "🎯" : "❄️";
            var percent = g.TargetAmount > 0 ? (g.CurrentAmount / g.TargetAmount) * 100 : 0;
            sb.AppendLine($"{icon} *{g.Name}* ({g.CurrentAmount:N0}/{g.TargetAmount:N0})");
        }

        sb.AppendLine("\n👇 *Выберите новую ГЛАВНУЮ цель:*");

        return (sb.ToString(), BotInlineKeyboards.GoalList(goals.ToList(), main?.Id ?? 0));
    }

    // Сцена 3b: Диалог переноса денег
    private (string, Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup) BuildTransfer(UserFlowState flow)
    {
        var sb = new StringBuilder();
        sb.AppendLine("🔄 *Смена фокуса*\n");
        sb.AppendLine($"На предыдущей цели лежат деньги.");
        sb.AppendLine("\nЧто сделать с накоплениями?");

        return (sb.ToString(), BotInlineKeyboards.GoalTransfer("новую цель", 0));
    }

    // Сцена 5: Победа
    private async Task<(string, Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup)> BuildVictoryAsync(long userId, CancellationToken ct)
    {
        var main = await goalService.GetActiveGoalAsync(userId, ct);
        if (main == null)
            return ("🎯 Нет активной цели.", BotInlineKeyboards.GoalEmpty());

        return BuildVictoryText(main);
    }

    private (string, Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup) BuildVictoryText(Domain.Entities.Goal goal)
    {
        var sb = new StringBuilder();
        sb.AppendLine("🎉 *ПОЗДРАВЛЯЮ! ЦЕЛЬ ДОСТИГНУТА!* 🏆\n");
        sb.AppendLine($"🎯 *{goal.Name}*");
        sb.AppendLine($"✅ Собрано: *{goal.CurrentAmount:N0}* из *{goal.TargetAmount:N0}* TJS\n");
        sb.AppendLine("💰 Деньги лежат в копилке. Что делаем?");

        return (sb.ToString(), BotInlineKeyboards.GoalVictory(goal.Id));
    }

    // Настройки
    private async Task<(string, Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup)> BuildSettingsAsync(long userId, CancellationToken ct)
    {
        var main = await goalService.GetActiveGoalAsync(userId, ct);
        if (main == null)
            return ("⚙️ Нет активной цели для настройки.", BotInlineKeyboards.GoalEmpty());

        var sb = new StringBuilder();
        sb.AppendLine($"⚙️ *Настройки: {main.Name}*\n");
        sb.AppendLine($"💰 Сумма: {main.TargetAmount:N0} TJS");
        if (main.Deadline.HasValue)
            sb.AppendLine($"📅 Дедлайн: {main.Deadline:dd.MM.yyyy}");
        sb.AppendLine("\n👇 Что изменить?");

        return (sb.ToString(), BotInlineKeyboards.GoalSettings(main.Id));
    }

    // Прогресс-бар (10 символов)
    private static string BuildProgressBar(decimal percent)
    {
        var filled = (int)(percent / 10);
        filled = Math.Clamp(filled, 0, 10);
        return "[" + new string('▓', filled) + new string('░', 10 - filled) + "]";
    }

    // Обработка пополнения
    public async Task<bool> HandleDepositAsync(ITelegramBotClient bot, long chatId, long userId, UserFlowState flow, decimal amount, CancellationToken ct, int? messageId)
    {
        var account = await accountService.GetUserAccountAsync(userId, ct);
        if (account == null || account.Balance < amount)
        {
            await bot.AnswerCallbackQueryAsync(flow.PendingMessageId?.ToString() ?? "", "❌ Недостаточно средств!", showAlert: true, cancellationToken: ct);
            return false;
        }

        var main = await goalService.GetActiveGoalAsync(userId, ct);
        if (main == null) return false;

        // Списываем со счета
        await accountService.UpdateBalanceAsync(account.Id, account.Balance - amount, ct);
        // Добавляем в цель
        await goalService.AddFundsAsync(userId, main.Id, amount, ct);

        // Проверяем победу
        main = await goalService.GetActiveGoalAsync(userId, ct);
        if (main != null && main.CurrentAmount >= main.TargetAmount)
        {
            flow.CurrentGoalScreen = GoalScreen.Victory;
        }
        else
        {
            flow.CurrentGoalScreen = GoalScreen.Main;
        }

        await RenderCurrentScreenAsync(bot, chatId, userId, flow, ct, messageId);
        return true;
    }

    // Обработка снятия
    public async Task<bool> HandleWithdrawAsync(ITelegramBotClient bot, long chatId, long userId, UserFlowState flow, decimal amount, CancellationToken ct, int? messageId)
    {
        var main = await goalService.GetActiveGoalAsync(userId, ct);
        if (main == null || main.CurrentAmount < amount)
        {
            await bot.AnswerCallbackQueryAsync(flow.PendingMessageId?.ToString() ?? "", "❌ В копилке недостаточно!", showAlert: true, cancellationToken: ct);
            return false;
        }

        var account = await accountService.GetUserAccountAsync(userId, ct);
        if (account == null) return false;

        // Снимаем из цели
        await goalService.WithdrawAsync(userId, main.Id, amount, ct);
        // Добавляем на счет
        await accountService.UpdateBalanceAsync(account.Id, account.Balance + amount, ct);

        flow.CurrentGoalScreen = GoalScreen.Main;
        await RenderCurrentScreenAsync(bot, chatId, userId, flow, ct, messageId);
        return true;
    }

    // Смена приоритета (выбор новой главной цели)
    public async Task HandleSelectGoalAsync(ITelegramBotClient bot, long chatId, long userId, UserFlowState flow, int newGoalId, CancellationToken ct, int? messageId)
    {
        var oldMain = await goalService.GetActiveGoalAsync(userId, ct);

        // Если на старой цели есть деньги - показываем диалог переноса
        if (oldMain != null && oldMain.CurrentAmount > 0 && oldMain.Id != newGoalId)
        {
            flow.OldGoalIdForTransfer = oldMain.Id;
            flow.PendingGoalId = newGoalId;
            flow.CurrentGoalScreen = GoalScreen.Transfer;
            await RenderCurrentScreenAsync(bot, chatId, userId, flow, ct, messageId);
            return;
        }

        // Просто меняем приоритет
        await goalService.SetActiveAsync(userId, newGoalId, ct);
        flow.CurrentGoalScreen = GoalScreen.Main;
        await RenderCurrentScreenAsync(bot, chatId, userId, flow, ct, messageId);
    }

    // Обработка переноса денег
    public async Task HandleTransferAsync(ITelegramBotClient bot, long chatId, long userId, UserFlowState flow, bool doTransfer, CancellationToken ct, int? messageId)
    {
        if (flow.OldGoalIdForTransfer.HasValue && flow.PendingGoalId.HasValue)
        {
            var oldGoal = await goalService.GetByIdAsync(userId, flow.OldGoalIdForTransfer.Value, ct);
            if (oldGoal != null && doTransfer && oldGoal.CurrentAmount > 0)
            {
                var amount = oldGoal.CurrentAmount;
                // Снимаем со старой
                await goalService.WithdrawAsync(userId, oldGoal.Id, amount, ct);
                // Добавляем в новую
                await goalService.AddFundsAsync(userId, flow.PendingGoalId.Value, amount, ct);
            }

            // Устанавливаем новую главную
            await goalService.SetActiveAsync(userId, flow.PendingGoalId.Value, ct);
        }

        flow.OldGoalIdForTransfer = null;
        flow.CurrentGoalScreen = GoalScreen.Main;
        await RenderCurrentScreenAsync(bot, chatId, userId, flow, ct, messageId);
    }
}
