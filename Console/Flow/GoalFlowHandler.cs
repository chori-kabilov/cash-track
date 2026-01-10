using System.Globalization;
using Console.Bot;
using Infrastructure.Services;
using Telegram.Bot;

namespace Console.Flow;

// Обработчик шагов создания целей
public class GoalFlowHandler(
    IGoalService goalService,
    ICategoryService categoryService,
    TransactionFlowHandler transactionHandler) : IFlowStepHandler
{
    private static readonly UserFlowStep[] HandledSteps = 
    {
        UserFlowStep.WaitingGoalName,
        UserFlowStep.WaitingGoalTarget,
        UserFlowStep.WaitingGoalDeadline,
        UserFlowStep.WaitingGoalDeposit
    };

    public bool CanHandle(UserFlowStep step) => HandledSteps.Contains(step);

    public async Task<bool> HandleAsync(ITelegramBotClient bot, long chatId, long userId, string text, 
        UserFlowState flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        return flow.Step switch
        {
            UserFlowStep.WaitingGoalName => await HandleGoalNameAsync(bot, chatId, text, flow, ct),
            UserFlowStep.WaitingGoalTarget => await HandleGoalTargetAsync(bot, chatId, text, flow, ct),
            UserFlowStep.WaitingGoalDeadline => await HandleGoalDeadlineAsync(bot, chatId, userId, text, flow, flowDict, ct),
            UserFlowStep.WaitingGoalDeposit => await HandleGoalDepositAsync(bot, chatId, userId, text, flow, flowDict, ct),
            _ => false
        };
    }

    // Шаг 1: Ввод названия цели
    private async Task<bool> HandleGoalNameAsync(ITelegramBotClient bot, long chatId, string text, UserFlowState flow, CancellationToken ct)
    {
        flow.PendingGoalName = text;
        flow.Step = UserFlowStep.WaitingGoalTarget;
        await bot.SendTextMessageAsync(chatId, "Введите сумму цели:", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
        return true;
    }

    // Шаг 2: Ввод целевой суммы
    private async Task<bool> HandleGoalTargetAsync(ITelegramBotClient bot, long chatId, string text, UserFlowState flow, CancellationToken ct)
    {
        if (!FlowHelper.TryParseAmount(text, out var goalAmount) || goalAmount <= 0)
        {
            await bot.SendTextMessageAsync(chatId, "❌ Неверная сумма.", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
            return true;
        }
        flow.PendingGoalTarget = goalAmount;
        flow.Step = UserFlowStep.WaitingGoalDeadline;
        await bot.SendTextMessageAsync(chatId, "Введите дедлайн (ДД.ММ.ГГГГ) или 'нет':", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
        return true;
    }

    // Шаг 3: Ввод дедлайна (или пропуск)
    private async Task<bool> HandleGoalDeadlineAsync(ITelegramBotClient bot, long chatId, long userId, string text, UserFlowState flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        DateTimeOffset? deadline = null;
        if (DateTime.TryParseExact(text, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            deadline = new DateTimeOffset(d, TimeSpan.Zero);
        else if (!text.Contains("нет", StringComparison.OrdinalIgnoreCase))
        {
            await bot.SendTextMessageAsync(chatId, "❌ Неверный формат.", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
            return true;
        }

        await goalService.CreateAsync(userId, flow.PendingGoalName!, flow.PendingGoalTarget, deadline, ct);
        flowDict.Remove(userId);
        await bot.SendTextMessageAsync(chatId, "✅ Цель создана!", replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
        return true;
    }

    // Пополнение цели
    private async Task<bool> HandleGoalDepositAsync(ITelegramBotClient bot, long chatId, long userId, string text, UserFlowState flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        if (!FlowHelper.TryParseAmount(text, out var amount) || amount <= 0)
        {
            await bot.SendTextMessageAsync(chatId, "❌ Неверная сумма.", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
            return true;
        }

        var cats = await categoryService.GetUserCategoriesAsync(userId, ct);
        var savings = cats.FirstOrDefault(c => c.Name == "Накопления" && c.Type == Domain.Enums.TransactionType.Expense) 
                      ?? cats.FirstOrDefault(c => c.Type == Domain.Enums.TransactionType.Expense);
        
        if (savings != null)
            await transactionHandler.AddTransactionWithDescriptionAsync(bot, chatId, userId, amount, savings.Id, Domain.Enums.TransactionType.Expense, "Пополнение цели", false, ct);

        await goalService.AddFundsAsync(userId, flow.PendingGoalId!.Value, amount, ct);
        flowDict.Remove(userId);

        var goal = (await goalService.GetUserGoalsAsync(userId, ct)).FirstOrDefault(g => g.Id == flow.PendingGoalId);
        var msg = $"✅ Пополнено на {amount:F2}!";
        if (goal?.IsCompleted == true) msg += $"\n🎉 Цель \"{goal.Name}\" достигнута!";
        
        await bot.SendTextMessageAsync(chatId, msg, replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
        return true;
    }
}
