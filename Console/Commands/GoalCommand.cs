using System.Text;
using Console.Bot.Keyboards;
using Console.Flow;
using Domain.Enums;
using Infrastructure.Services;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace Console.Commands;

// Команда управления Целями (v3 — полные сценарии)
public class GoalCommand(
    IGoalService goalService, 
    IAccountService accountService,
    ITransactionService transactionService,
    ICategoryService categoryService)
{
    // Названия дефолтных категорий для целей
    private const string DepositCategoryName = "→ Цели";
    private const string WithdrawCategoryName = "← Из целей";

    // Точка входа
    // Точка входа
    public async Task ExecuteAsync(ITelegramBotClient bot, long chatId, long userId, UserFlowState flow, CancellationToken ct, int? messageId = null, string? callbackQueryId = null)
    {
        await ShowMainAsync(bot, chatId, userId, messageId, ct, callbackQueryId);
    }

    // === ЭКРАНЫ ===

    // Главная карточка
    public async Task ShowMainAsync(ITelegramBotClient bot, long chatId, long userId, int? msgId, CancellationToken ct, string? callbackQueryId = null, string? headerText = null)
    {
        var goals = await goalService.GetUserGoalsAsync(userId, ct);
        
        if (!goals.Any())
        {
            var completed = await goalService.GetCompletedAsync(userId, ct);
            if (completed.Any())
            {
                await CommandHelpers.SendOrEditAsync(bot, chatId, msgId, 
                    "🎊 *Все цели достигнуты!*\n\nВы прошли все свои финансовые цели!\nСоздайте новую, чтобы продолжить копить.", 
                    GoalKeyboards.AllCompleted(), ct, callbackQueryId);
            }
            else
            {
                await CommandHelpers.SendOrEditAsync(bot, chatId, msgId, 
                    "🎯 *Копилка пуста*\n\nУ вас пока нет финансовых целей.\nСоздайте первую!", 
                    GoalKeyboards.Empty(), ct, callbackQueryId);
            }
            return;
        }

        var main = goals.OrderBy(g => g.Priority).FirstOrDefault(g => g.IsActive);
        if (main == null)
        {
            // Если есть цели, но нет активной — делаем первую активной
            main = goals.First();
            await goalService.SetActiveAsync(userId, main.Id, ct);
        }

        if (main.CurrentAmount >= main.TargetAmount)
        {
            // ShowVictory does not support nullable msgId yet, need to check or update calls
            // For now, let's assume ShowVictory is called usually with msgId
            // But wait, if ShowMainAsync is called with null msgId, ShowVictory needs to handle it. 
            // Let's defer strict check or use SendOrEditAsync there too? 
            // ShowVictoryAsync signature in file is `int msgId`. I should update it too or handle it here.
            
            // Let's try to update ShowVictoryAsync signature as well in a separate chunk.
            await ShowVictoryAsync(bot, chatId, userId, main.Id, msgId, ct, callbackQueryId);
            return;
        }

        var text = BuildGoalCard(main);
        if (!string.IsNullOrEmpty(headerText))
            text = headerText + "\n\n" + text;

        await CommandHelpers.SendOrEditAsync(bot, chatId, msgId, text, 
            GoalKeyboards.MainKeyboard(), ct, callbackQueryId);
    }

    // После создания цели
    public async Task ShowAfterCreateAsync(ITelegramBotClient bot, long chatId, Domain.Entities.Goal goal, bool isFirst, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine("✅ *Цель создана!*\n");
        sb.AppendLine($"🎯 *{goal.Name}*\n");
        sb.AppendLine($"💰 Цель: *{goal.TargetAmount:N0}* TJS");
        sb.AppendLine($"📊 {BuildProgressBar(0)} *0%*");
        sb.AppendLine($"⏳ Осталось: *{goal.TargetAmount:N0}* TJS");
        
        if (goal.Deadline.HasValue)
        {
            var daysLeft = Math.Max(0, (goal.Deadline.Value - DateTimeOffset.UtcNow).Days);
            sb.AppendLine($"\n📅 Дедлайн: {goal.Deadline:dd.MM.yyyy} ({daysLeft} дн.)");
        }

        await bot.SendTextMessageAsync(chatId, sb.ToString(), ParseMode.Markdown, 
            replyMarkup: GoalKeyboards.AfterCreate(goal.Id, isFirst), cancellationToken: ct);
    }

    // Экран пополнения
    public async Task ShowDepositAsync(ITelegramBotClient bot, long chatId, long userId, int msgId, CancellationToken ct, string? callbackQueryId = null)
    {
        var main = await goalService.GetActiveGoalAsync(userId, ct);
        var account = await accountService.GetUserAccountAsync(userId, ct);
        var balance = account?.Balance ?? 0;
        var remaining = main != null ? main.TargetAmount - main.CurrentAmount : 0;

        var sb = new StringBuilder();
        sb.AppendLine($"💳 *Пополнение: {main?.Name ?? "Цель"}*\n");
        sb.AppendLine($"💰 Баланс: *{balance:N0}* TJS");
        sb.AppendLine($"🎯 В копилке: *{main?.CurrentAmount:N0}* TJS");
        sb.AppendLine($"⏳ Осталось до цели: *{remaining:N0}* TJS");

        if (balance <= 0)
            sb.AppendLine("\n❌ Нет свободных средств");
        else
            sb.AppendLine("\n👇 Нажмите кнопку или введите сумму:");

        var suggested = GoalKeyboards.CalculateSmartDeposit(balance, remaining);
        await CommandHelpers.SafeEditMessageAsync(bot, chatId, msgId, sb.ToString(), 
            GoalKeyboards.Deposit(suggested), ct, callbackQueryId);
    }

    // Настройки
    public async Task ShowSettingsAsync(ITelegramBotClient bot, long chatId, long userId, int msgId, CancellationToken ct, string? callbackQueryId = null, int? goalId = null)
    {
        Domain.Entities.Goal? main;
        if (goalId.HasValue)
            main = await goalService.GetByIdAsync(userId, goalId.Value, ct);
        else
            main = await goalService.GetActiveGoalAsync(userId, ct);

        if (main == null) { await ShowMainAsync(bot, chatId, userId, msgId, ct, callbackQueryId); return; }

        var sb = new StringBuilder();
        sb.AppendLine($"⚙️ *Настройки: {main.Name}*\n");
        sb.AppendLine($"💰 Цель: *{main.TargetAmount:N0}* TJS");
        sb.AppendLine($"🎯 Накоплено: *{main.CurrentAmount:N0}* TJS");
        if (main.Deadline.HasValue)
            sb.AppendLine($"📅 Дедлайн: *{main.Deadline:dd.MM.yyyy}*");

        await CommandHelpers.SafeEditMessageAsync(bot, chatId, msgId, sb.ToString(), 
            GoalKeyboards.Settings(main.Id), ct, callbackQueryId);
    }

    // Снятие
    public async Task ShowWithdrawAsync(ITelegramBotClient bot, long chatId, long userId, int msgId, CancellationToken ct, string? callbackQueryId = null)
    {
        var main = await goalService.GetActiveGoalAsync(userId, ct);
        var inGoal = main?.CurrentAmount ?? 0;

        var sb = new StringBuilder();
        sb.AppendLine($"💸 *Снятие: {main?.Name ?? "Цель"}*\n");
        sb.AppendLine($"🎯 В копилке: *{inGoal:N0}* TJS");

        if (inGoal <= 0)
            sb.AppendLine("\n❌ Копилка пуста");
        else
        {
            sb.AppendLine("\n⚠️ Это отодвинет дату покупки!");
            sb.AppendLine("👇 Нажмите кнопку или введите сумму:");
        }

        var suggested = GoalKeyboards.CalculateSmartDeposit(inGoal, inGoal);
        await CommandHelpers.SafeEditMessageAsync(bot, chatId, msgId, sb.ToString(), 
            GoalKeyboards.Withdraw(suggested), ct, callbackQueryId);
    }

    // Список целей с пагинацией
    public async Task ShowListAsync(ITelegramBotClient bot, long chatId, long userId, int msgId, int page, CancellationToken ct, string? callbackQueryId = null)
    {
        var goals = await goalService.GetUserGoalsAsync(userId, ct);
        var main = await goalService.GetActiveGoalAsync(userId, ct);

        if (!goals.Any())
        {
            await CommandHelpers.SafeEditMessageAsync(bot, chatId, msgId, 
                "📋 *Ваши цели*\n\n_Пусто. Создайте первую цель!_", 
                GoalKeyboards.Empty(), ct, callbackQueryId);
            return;
        }

        int pageSize = 5;
        var totalPages = (int)Math.Ceiling((double)goals.Count / pageSize);
        // Ensure page is within bounds
        if (page < 0) page = 0;
        if (page >= totalPages && totalPages > 0) page = totalPages - 1;

        var sb = new StringBuilder();
        sb.AppendLine("📋 *Ваши цели*");
        sb.AppendLine($"*Страница {page + 1} из {totalPages}*\n");

        var pageGoals = goals.Skip(page * pageSize).Take(pageSize).ToList();
        var startNum = page * pageSize + 1;

        foreach (var (g, idx) in pageGoals.Select((g, i) => (g, i)))
        {
            var num = startNum + idx;
            var icon = g.Id == (main?.Id ?? 0) ? "🎯" : "❄️";
            var percent = g.TargetAmount > 0 ? (g.CurrentAmount / g.TargetAmount) * 100 : 0;
            
            sb.AppendLine($"{num}. {icon} *{g.Name}*");
            sb.AppendLine($"   💰 {g.CurrentAmount:N0} / {g.TargetAmount:N0} TJS ({percent:N0}%)");
            sb.AppendLine();
        }

        sb.AppendLine("👇 *Введите номер цели, чтобы сделать её главной:*");

        await CommandHelpers.SafeEditMessageAsync(bot, chatId, msgId, sb.ToString(), 
            GoalKeyboards.List(page, totalPages), ct, callbackQueryId);
    }

    // Победа
    public async Task ShowVictoryAsync(ITelegramBotClient bot, long chatId, long userId, int goalId, int? msgId, CancellationToken ct, string? callbackQueryId = null)
    {
        var goal = await goalService.GetByIdAsync(userId, goalId, ct);
        if (goal == null) return;

        var sb = new StringBuilder();
        sb.AppendLine("🎉 *ЦЕЛЬ ДОСТИГНУТА!*\n");
        sb.AppendLine($"🏆 *{goal.Name}*");
        sb.AppendLine($"💰 Накоплено: *{goal.CurrentAmount:N0}* TJS из *{goal.TargetAmount:N0}* TJS\n");
        sb.AppendLine("Поздравляем! Вы молодец! 🎊\nЧто делаем с накоплениями?");

        await CommandHelpers.SendOrEditAsync(bot, chatId, msgId, sb.ToString(), 
            GoalKeyboards.Victory(goalId), ct, callbackQueryId);
    }

    // Подтверждение удаления
    public async Task ShowDeleteConfirmAsync(ITelegramBotClient bot, long chatId, long userId, int goalId, int msgId, CancellationToken ct, string? callbackQueryId = null)
    {
        var goal = await goalService.GetByIdAsync(userId, goalId, ct);
        if (goal == null) return;

        var sb = new StringBuilder();
        sb.AppendLine($"🗑 *Удаление: {goal.Name}*\n");
        if (goal.CurrentAmount > 0)
            sb.AppendLine($"⚠️ В копилке: *{goal.CurrentAmount:N0}* TJS\nЭти деньги вернутся на баланс.\n");
        sb.AppendLine("Подтвердить удаление?");

        await CommandHelpers.SafeEditMessageAsync(bot, chatId, msgId, sb.ToString(), 
            GoalKeyboards.DeleteConfirm(goalId), ct, callbackQueryId);
    }

    // Выбор цели для переполнения
    public async Task ShowOverflowTargetsAsync(ITelegramBotClient bot, long chatId, long userId, decimal amount, int msgId, CancellationToken ct, string? callbackQueryId = null)
    {
        var goals = await goalService.GetUserGoalsAsync(userId, ct);
        var active = goals.Where(g => g.IsActive && g.CurrentAmount < g.TargetAmount).ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"💰 *Перевести {amount:N0} TJS*\n");
        sb.AppendLine("Выберите цель:");

        await CommandHelpers.SafeEditMessageAsync(bot, chatId, msgId, sb.ToString(), 
            GoalKeyboards.OverflowTargets(active, amount), ct, callbackQueryId);
    }

    // === ДЕЙСТВИЯ ===

    // Пополнение
    public async Task<bool> DepositAsync(ITelegramBotClient bot, long chatId, long userId, decimal amount, int? msgId, CancellationToken ct, string? callbackQueryId = null)
    {
        var account = await accountService.GetUserAccountAsync(userId, ct);
        if (account == null || account.Balance < amount)
        {
            await bot.SendTextMessageAsync(chatId, "❌ Недостаточно средств!", replyMarkup: GoalKeyboards.Cancel(), cancellationToken: ct);
            return false;
        }

        var main = await goalService.GetActiveGoalAsync(userId, ct);
        if (main == null) return false;

        var remaining = main.TargetAmount - main.CurrentAmount;
        var actualDeposit = Math.Min(amount, remaining);
        var excess = amount - actualDeposit;

        // Транзакция
        var depositCat = await EnsureGoalCategoryAsync(userId, DepositCategoryName, TransactionType.Expense, ct);
        if (depositCat != null)
            await transactionService.ProcessTransactionAsync(userId, depositCat.Id, actualDeposit, TransactionType.Expense, $"→ {main.Name}", false, null, ct);

        // Добавить в цель
        await goalService.AddFundsAsync(userId, main.Id, actualDeposit, ct);
        main = await goalService.GetActiveGoalAsync(userId, ct);

        // Результат
        var sb = new StringBuilder();
        sb.AppendLine($"✅ *+{actualDeposit:N0} TJS* отправлено в копилку!\n");

        if (main != null)
        {
            var percent = main.TargetAmount > 0 ? (main.CurrentAmount / main.TargetAmount) * 100 : 0;
            var left = main.TargetAmount - main.CurrentAmount;
            sb.AppendLine($"🎯 *{main.Name}*\n");
            sb.AppendLine($"💰 Накоплено: *{main.CurrentAmount:N0}* TJS");
            sb.AppendLine($"🏁 Цель: *{main.TargetAmount:N0}* TJS");
            sb.AppendLine($"📊 {BuildProgressBar(percent)} *{percent:N0}%*");
            if (left > 0) sb.AppendLine($"⏳ Осталось: *{left:N0}* TJS");
        }

        // Переполнение
        if (excess > 0 && main != null && main.CurrentAmount >= main.TargetAmount)
        {
            sb.AppendLine($"\n🎉 *ЦЕЛЬ ДОСТИГНУТА!*");
            sb.AppendLine($"\n💡 У вас осталось *{excess:N0}* TJS.\nОтправить в другую цель?");
            await bot.SendTextMessageAsync(chatId, sb.ToString(), ParseMode.Markdown, 
                replyMarkup: GoalKeyboards.VictoryWithOverflow(main.Id, excess), cancellationToken: ct);
            return true;
        }

        // Победа без переполнения
        if (main != null && main.CurrentAmount >= main.TargetAmount)
        {
            sb.AppendLine("\n🎉 *ЦЕЛЬ ДОСТИГНУТА!*");
            if (msgId.HasValue)
                await CommandHelpers.SafeEditMessageAsync(bot, chatId, msgId.Value, sb.ToString(), 
                    GoalKeyboards.Victory(main.Id), ct, callbackQueryId);
            else
                await bot.SendTextMessageAsync(chatId, sb.ToString(), ParseMode.Markdown, 
                    replyMarkup: GoalKeyboards.Victory(main.Id), cancellationToken: ct);
            return true;
        }

        // Обычное пополнение
        if (msgId.HasValue)
            await CommandHelpers.SafeEditMessageAsync(bot, chatId, msgId.Value, sb.ToString(), 
                GoalKeyboards.MainKeyboard(), ct, callbackQueryId);
        else
            await bot.SendTextMessageAsync(chatId, sb.ToString(), ParseMode.Markdown, 
                replyMarkup: GoalKeyboards.MainKeyboard(), cancellationToken: ct);
        return true;
    }

    // Снятие
    public async Task<bool> WithdrawAsync(ITelegramBotClient bot, long chatId, long userId, decimal amount, int? msgId, CancellationToken ct, string? callbackQueryId = null)
    {
        var main = await goalService.GetActiveGoalAsync(userId, ct);
        if (main == null || main.CurrentAmount < amount)
        {
            await bot.SendTextMessageAsync(chatId, "❌ В копилке недостаточно!", replyMarkup: GoalKeyboards.Cancel(), cancellationToken: ct);
            return false;
        }

        var account = await accountService.GetUserAccountAsync(userId, ct);
        if (account == null) return false;

        await goalService.WithdrawAsync(userId, main.Id, amount, ct);
        
        // Транзакция (доход) — она сама обновит баланс
        var withdrawCat = await EnsureGoalCategoryAsync(userId, WithdrawCategoryName, TransactionType.Income, ct);
        if (withdrawCat != null)
            await transactionService.ProcessTransactionAsync(userId, withdrawCat.Id, amount, TransactionType.Income, $"← {main.Name}", false, null, ct);

        // Обновим объект аккаунта для отображения
        account = await accountService.GetUserAccountAsync(userId, ct);
        if (account == null) return false;

        main = await goalService.GetActiveGoalAsync(userId, ct);
        var sb = new StringBuilder();
        sb.AppendLine($"✅ *-{amount:N0} TJS* снято из копилки\n");
        sb.AppendLine($"💰 Ваш баланс: *{account.Balance + amount:N0}* TJS");
        if (main != null)
        {
            var percent = main.TargetAmount > 0 ? (main.CurrentAmount / main.TargetAmount) * 100 : 0;
            sb.AppendLine($"\n🎯 {main.Name}: *{main.CurrentAmount:N0}* TJS ({percent:N0}%)");
        }

        if (msgId.HasValue)
            await CommandHelpers.SafeEditMessageAsync(bot, chatId, msgId.Value, sb.ToString(), 
                GoalKeyboards.MainKeyboard(), ct, callbackQueryId);
        else
            await bot.SendTextMessageAsync(chatId, sb.ToString(), ParseMode.Markdown, 
                replyMarkup: GoalKeyboards.MainKeyboard(), cancellationToken: ct);
        return true;
    }

    // Переполнение в другую цель
    public async Task TransferOverflowAsync(ITelegramBotClient bot, long chatId, long userId, int targetGoalId, decimal amount, int msgId, CancellationToken ct, string? callbackQueryId = null)
    {
        var goal = await goalService.GetByIdAsync(userId, targetGoalId, ct);
        if (goal == null) return;

        await goalService.AddFundsAsync(userId, targetGoalId, amount, ct);
        
        var depositCat = await EnsureGoalCategoryAsync(userId, DepositCategoryName, TransactionType.Expense, ct);
        if (depositCat != null)
            await transactionService.ProcessTransactionAsync(userId, depositCat.Id, amount, TransactionType.Expense, $"→ {goal.Name}", false, null, ct);

        goal = await goalService.GetByIdAsync(userId, targetGoalId, ct);
        var percent = goal!.TargetAmount > 0 ? (goal.CurrentAmount / goal.TargetAmount) * 100 : 0;

        await CommandHelpers.SafeEditMessageAsync(bot, chatId, msgId, 
            $"✅ *+{amount:N0} TJS* → {goal.Name}\n\n🎯 {goal.Name}: {percent:N0}%", 
            GoalKeyboards.MainKeyboard(), ct, callbackQueryId);
    }

    // Выбор цели
    public async Task SelectGoalAsync(ITelegramBotClient bot, long chatId, long userId, int goalId, int msgId, CancellationToken ct, string? callbackQueryId = null)
    {
        await goalService.SetActiveAsync(userId, goalId, ct);
        await ShowMainAsync(bot, chatId, userId, msgId, ct, callbackQueryId);
    }

    // Сделать главной
    public async Task SetMainAsync(ITelegramBotClient bot, long chatId, long userId, int goalId, int msgId, CancellationToken ct, string? callbackQueryId = null)
    {
        await goalService.SetActiveAsync(userId, goalId, ct);
        await ShowMainAsync(bot, chatId, userId, msgId, ct, callbackQueryId);
    }

    // Купил! (Списать)
    public async Task BoughtAsync(ITelegramBotClient bot, long chatId, long userId, int goalId, int msgId, CancellationToken ct, string? callbackQueryId = null)
    {
        var goal = await goalService.GetByIdAsync(userId, goalId, ct);
        if (goal == null) return;

        // 1. Записать расход
        var cats = await categoryService.GetUserCategoriesAsync(userId, ct);
        var cat = cats.FirstOrDefault(c => c.Type == TransactionType.Expense);
        if (cat != null)
            await transactionService.ProcessTransactionAsync(userId, cat.Id, goal.CurrentAmount, TransactionType.Expense, $"Покупка: {goal.Name}", false, null, ct);

        // 2. Завершить цель
        await goalService.CompleteAsync(userId, goalId, ct);

        // 3. Найти следующую цель
        var remainingGoals = await goalService.GetUserGoalsAsync(userId, ct);
        var nextGoal = remainingGoals.FirstOrDefault();
        
        if (nextGoal != null)
        {
            await goalService.SetActiveAsync(userId, nextGoal.Id, ct);
            nextGoal = await goalService.GetByIdAsync(userId, nextGoal.Id, ct); // Обновить данные
        }

        var sb = new StringBuilder();
        sb.AppendLine("🎊 *Поздравляем с покупкой!*\n");
        sb.AppendLine($"✅ {goal.Name} — теперь ваш!");
        sb.AppendLine($"-{goal.CurrentAmount:N0} TJS списано");

        if (nextGoal != null)
        {
            var percent = nextGoal.TargetAmount > 0 ? (nextGoal.CurrentAmount / nextGoal.TargetAmount) * 100 : 0;
            sb.AppendLine($"\n*Следующая цель:* 🎯 {nextGoal.Name}");
            sb.AppendLine($"💰 {nextGoal.CurrentAmount:N0} TJS из {nextGoal.TargetAmount:N0} TJS");
            sb.AppendLine($"📊 {BuildProgressBar(percent)} {percent:N0}%");
        }
        else
        {
            sb.AppendLine("\n🎉 *Все цели достигнуты!*");
            sb.AppendLine("Вы прошли все финансовые цели! Создайте новую.");
        }

        await CommandHelpers.SafeEditMessageAsync(bot, chatId, msgId, sb.ToString(), 
            GoalKeyboards.AfterBought(nextGoal != null), ct, callbackQueryId);
    }

    // Удаление
    public async Task DeleteGoalAsync(ITelegramBotClient bot, long chatId, long userId, int goalId, int msgId, CancellationToken ct, string? callbackQueryId = null)
    {
        var goal = await goalService.GetByIdAsync(userId, goalId, ct);
        if (goal == null) return;

        if (goal.CurrentAmount > 0)
        {
            var account = await accountService.GetUserAccountAsync(userId, ct);
            if (account != null)
            {
                var withdrawCat = await EnsureGoalCategoryAsync(userId, WithdrawCategoryName, TransactionType.Income, ct);
                if (withdrawCat != null)
                    await transactionService.ProcessTransactionAsync(userId, withdrawCat.Id, goal.CurrentAmount, TransactionType.Income, $"← Удалено: {goal.Name}", false, null, ct);
            }
        }

        await goalService.DeleteAsync(userId, goalId, ct);

        var sb = new StringBuilder();
        sb.AppendLine($"🗑 Цель «{goal.Name}» удалена.");
        if (goal.CurrentAmount > 0)
            sb.AppendLine($"+{goal.CurrentAmount:N0} TJS возвращено на баланс.");

        // Получаем следующую активную цель, чтобы показать пользователю
        var remainingGoals = await goalService.GetUserGoalsAsync(userId, ct);
        var nextActive = remainingGoals.FirstOrDefault(g => g.IsActive);
        if (nextActive == null && remainingGoals.Any())
        {
            nextActive = remainingGoals.First();
            await goalService.SetActiveAsync(userId, nextActive.Id, ct);
        }

        if (nextActive != null)
        {
             sb.AppendLine($"\n✅ *{nextActive.Name}* — теперь главная цель!");
        }

        await CommandHelpers.SafeEditMessageAsync(bot, chatId, msgId, sb.ToString(), 
            GoalKeyboards.MainKeyboard(), ct, callbackQueryId);
    }

    // === ХЕЛПЕРЫ ===

    private async Task<Domain.Entities.Category?> EnsureGoalCategoryAsync(long userId, string name, TransactionType type, CancellationToken ct)
    {
        var cats = await categoryService.GetUserCategoriesAsync(userId, ct);
        var cat = cats.FirstOrDefault(c => c.Name == name && c.Type == type);
        if (cat == null)
        {
            await categoryService.CreateAsync(userId, name, type, null, ct);
            cats = await categoryService.GetUserCategoriesAsync(userId, ct);
            cat = cats.FirstOrDefault(c => c.Name == name && c.Type == type);
        }
        return cat;
    }

    private string BuildGoalCard(Domain.Entities.Goal goal)
    {
        var sb = new StringBuilder();
        var percent = goal.TargetAmount > 0 ? (goal.CurrentAmount / goal.TargetAmount) * 100 : 0;
        var remaining = goal.TargetAmount - goal.CurrentAmount;

        sb.AppendLine($"🎯 *{goal.Name}*\n");
        sb.AppendLine($"💰 Накоплено: *{goal.CurrentAmount:N0}* TJS");
        sb.AppendLine($"🏁 Цель: *{goal.TargetAmount:N0}* TJS");
        sb.AppendLine($"📊 {BuildProgressBar(percent)} *{percent:N0}%*");
        sb.AppendLine($"⏳ Осталось: *{remaining:N0}* TJS");

        if (goal.Deadline.HasValue)
        {
            var daysLeft = Math.Max(0, (goal.Deadline.Value - DateTimeOffset.UtcNow).Days);
            sb.AppendLine($"\n📅 Дедлайн: {goal.Deadline:dd.MM.yyyy} ({daysLeft} дн.)");
            if (daysLeft > 0 && remaining > 0)
                sb.AppendLine($"💡 По *{remaining / daysLeft:N0}* в день");
        }
        return sb.ToString();
    }

    private static string BuildProgressBar(decimal percent)
    {
        var filled = Math.Clamp((int)(percent / 10), 0, 10);
        return "[" + new string('▓', filled) + new string('░', 10 - filled) + "]";
    }
}
