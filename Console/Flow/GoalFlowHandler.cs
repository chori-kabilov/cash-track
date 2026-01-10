using System.Globalization;
using Console.Bot;
using Console.Bot.Keyboards;
using Console.Commands;
using Infrastructure.Services;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace Console.Flow;

// Обработчик текстового ввода для Целей (v3 — полные сценарии)
public class GoalFlowHandler(
    IGoalService goalService,
    GoalCommand goalCmd) : IFlowStepHandler
{
    private static readonly UserFlowStep[] HandledSteps = 
    {
        UserFlowStep.WaitingGoalName,
        UserFlowStep.WaitingGoalTarget,
        UserFlowStep.WaitingGoalDeadline,
        UserFlowStep.WaitingGoalDeposit,
        UserFlowStep.WaitingGoalWithdraw,
        UserFlowStep.WaitingGoalSelect,
        UserFlowStep.WaitingGoalEditName,
        UserFlowStep.WaitingGoalEditAmount,
        UserFlowStep.WaitingGoalEditDeadline
    };

    public bool CanHandle(UserFlowStep step) => HandledSteps.Contains(step);

    public async Task<bool> HandleAsync(ITelegramBotClient bot, long chatId, long userId, string text, 
        UserFlowState flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        return flow.Step switch
        {
            UserFlowStep.WaitingGoalName => await HandleNameAsync(bot, chatId, userId, text, flow, ct),
            UserFlowStep.WaitingGoalTarget => await HandleTargetAsync(bot, chatId, text, flow, ct),
            UserFlowStep.WaitingGoalDeadline => await HandleDeadlineAsync(bot, chatId, userId, text, flow, flowDict, ct),
            UserFlowStep.WaitingGoalDeposit => await HandleDepositAsync(bot, chatId, userId, text, flowDict, ct),
            UserFlowStep.WaitingGoalWithdraw => await HandleWithdrawAsync(bot, chatId, userId, text, flowDict, ct),
            UserFlowStep.WaitingGoalSelect => await HandleSelectAsync(bot, chatId, userId, text, flowDict, ct),
            UserFlowStep.WaitingGoalEditName => await HandleEditNameAsync(bot, chatId, userId, text, flow, flowDict, ct),
            UserFlowStep.WaitingGoalEditAmount => await HandleEditAmountAsync(bot, chatId, userId, text, flow, flowDict, ct),
            UserFlowStep.WaitingGoalEditDeadline => await HandleEditDeadlineAsync(bot, chatId, userId, text, flow, flowDict, ct),
            _ => false
        };
    }

    // Название новой цели
    private async Task<bool> HandleNameAsync(ITelegramBotClient bot, long chatId, long userId, string text, UserFlowState flow, CancellationToken ct)
    {
        flow.PendingGoalName = text.Trim();
        flow.Step = UserFlowStep.WaitingGoalTarget;
        
        await bot.SendTextMessageAsync(chatId, 
            $"📝 Цель: *{flow.PendingGoalName}*\n\nВведите сумму (в TJS):", 
            ParseMode.Markdown, replyMarkup: GoalKeyboards.Cancel(), cancellationToken: ct);
        return true;
    }

    // Сумма новой цели
    private async Task<bool> HandleTargetAsync(ITelegramBotClient bot, long chatId, string text, UserFlowState flow, CancellationToken ct)
    {
        if (!FlowHelper.TryParseAmount(text, out var amount) || amount <= 0)
        {
            await bot.SendTextMessageAsync(chatId, "❌ Введите число:", 
                replyMarkup: GoalKeyboards.Cancel(), cancellationToken: ct);
            return true;
        }

        flow.PendingGoalTarget = amount;
        flow.Step = UserFlowStep.WaitingGoalDeadline;
        
        await bot.SendTextMessageAsync(chatId, 
            $"💰 Сумма: *{amount:N0}* TJS\n\nДедлайн? (ДД.ММ.ГГГГ или «нет»):", 
            ParseMode.Markdown, replyMarkup: GoalKeyboards.Cancel(), cancellationToken: ct);
        return true;
    }

    // Дедлайн новой цели
    private async Task<bool> HandleDeadlineAsync(ITelegramBotClient bot, long chatId, long userId, string text, UserFlowState flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        DateTimeOffset? deadline = null;
        
        if (!text.Contains("нет", StringComparison.OrdinalIgnoreCase))
        {
            if (DateTime.TryParseExact(text.Trim(), "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                deadline = new DateTimeOffset(d, TimeSpan.Zero);
            else
            {
                await bot.SendTextMessageAsync(chatId, "❌ Формат: ДД.ММ.ГГГГ (или «нет»):", 
                    replyMarkup: GoalKeyboards.Cancel(), cancellationToken: ct);
                return true;
            }
        }

        var goals = await goalService.GetUserGoalsAsync(userId, ct);
        var isFirst = !goals.Any();
        
        var goal = await goalService.CreateAsync(userId, flow.PendingGoalName!, flow.PendingGoalTarget, deadline, ct);
        flowDict.Remove(userId);
        
        await goalCmd.ShowAfterCreateAsync(bot, chatId, goal, isFirst, ct);
        return true;
    }

    // Пополнение (пользовательский ввод)
    private async Task<bool> HandleDepositAsync(ITelegramBotClient bot, long chatId, long userId, string text, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        if (!FlowHelper.TryParseAmount(text, out var amount) || amount <= 0)
        {
            await bot.SendTextMessageAsync(chatId, "❌ Введите сумму:", 
                replyMarkup: GoalKeyboards.Cancel(), cancellationToken: ct);
            return true;
        }

        flowDict.Remove(userId);
        await goalCmd.DepositAsync(bot, chatId, userId, amount, null, ct);
        return true;
    }

    // Снятие (пользовательский ввод)
    private async Task<bool> HandleWithdrawAsync(ITelegramBotClient bot, long chatId, long userId, string text, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        if (!FlowHelper.TryParseAmount(text, out var amount) || amount <= 0)
        {
            await bot.SendTextMessageAsync(chatId, "❌ Введите сумму:", 
                replyMarkup: GoalKeyboards.Cancel(), cancellationToken: ct);
            return true;
        }

        flowDict.Remove(userId);
        await goalCmd.WithdrawAsync(bot, chatId, userId, amount, null, ct);
        return true;
    }

    // Выбор цели по номеру
    private async Task<bool> HandleSelectAsync(ITelegramBotClient bot, long chatId, long userId, string text, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        if (!int.TryParse(text.Trim(), out var num) || num < 1)
        {
            await bot.SendTextMessageAsync(chatId, "❌ Введите номер цели:", 
                replyMarkup: GoalKeyboards.Cancel(), cancellationToken: ct);
            return true;
        }

        var goals = await goalService.GetUserGoalsAsync(userId, ct);
        if (num > goals.Count)
        {
            await bot.SendTextMessageAsync(chatId, $"❌ Нет цели с номером {num}", 
                replyMarkup: GoalKeyboards.Cancel(), cancellationToken: ct);
            return true;
        }

        var goal = goals[num - 1];
        await goalService.SetActiveAsync(userId, goal.Id, ct);
        flowDict.Remove(userId);
        
        await bot.SendTextMessageAsync(chatId, 
            $"✅ *{goal.Name}* — теперь главная цель!", 
            ParseMode.Markdown, replyMarkup: GoalKeyboards.MainKeyboard(), cancellationToken: ct);
        return true;
    }

    // Редактирование названия
    private async Task<bool> HandleEditNameAsync(ITelegramBotClient bot, long chatId, long userId, string text, UserFlowState flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        if (flow.PendingGoalId == null) return false;
        
        await goalService.UpdateNameAsync(userId, flow.PendingGoalId.Value, text.Trim(), ct);
        flowDict.Remove(userId);
        
        await bot.SendTextMessageAsync(chatId, 
            $"✅ Название изменено на *{text.Trim()}*", 
            ParseMode.Markdown, replyMarkup: GoalKeyboards.MainKeyboard(), cancellationToken: ct);
        return true;
    }

    // Редактирование суммы
    private async Task<bool> HandleEditAmountAsync(ITelegramBotClient bot, long chatId, long userId, string text, UserFlowState flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        if (flow.PendingGoalId == null) return false;
        
        if (!FlowHelper.TryParseAmount(text, out var amount) || amount <= 0)
        {
            await bot.SendTextMessageAsync(chatId, "❌ Введите число:", 
                replyMarkup: GoalKeyboards.Cancel(), cancellationToken: ct);
            return true;
        }

        await goalService.UpdateTargetAsync(userId, flow.PendingGoalId.Value, amount, ct);
        flowDict.Remove(userId);
        
        await bot.SendTextMessageAsync(chatId, 
            $"✅ Сумма изменена на *{amount:N0}* TJS", 
            ParseMode.Markdown, replyMarkup: GoalKeyboards.MainKeyboard(), cancellationToken: ct);
        return true;
    }

    // Редактирование дедлайна
    private async Task<bool> HandleEditDeadlineAsync(ITelegramBotClient bot, long chatId, long userId, string text, UserFlowState flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        if (flow.PendingGoalId == null) return false;
        
        DateTimeOffset? deadline = null;
        if (!text.Contains("нет", StringComparison.OrdinalIgnoreCase))
        {
            if (DateTime.TryParseExact(text.Trim(), "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                deadline = new DateTimeOffset(d, TimeSpan.Zero);
            else
            {
                await bot.SendTextMessageAsync(chatId, "❌ Формат: ДД.ММ.ГГГГ (или «нет»):", 
                    replyMarkup: GoalKeyboards.Cancel(), cancellationToken: ct);
                return true;
            }
        }

        await goalService.UpdateDeadlineAsync(userId, flow.PendingGoalId.Value, deadline, ct);
        flowDict.Remove(userId);
        
        var msg = deadline.HasValue ? $"✅ Дедлайн: *{deadline:dd.MM.yyyy}*" : "✅ Дедлайн убран";
        await bot.SendTextMessageAsync(chatId, msg, ParseMode.Markdown, 
            replyMarkup: GoalKeyboards.MainKeyboard(), cancellationToken: ct);
        return true;
    }
}
