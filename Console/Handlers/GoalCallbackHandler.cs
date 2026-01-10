using Console.Bot.Keyboards;
using Console.Commands;
using Console.Flow;
using Infrastructure.Services;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Console.Handlers;

// Обработчик callback-кнопок для Целей (v3 — полные сценарии)
public class GoalCallbackHandler(
    GoalCommand goalCmd,
    IGoalService goalService) : ICallbackHandler
{
    public async Task<bool> HandleAsync(ITelegramBotClient bot, CallbackQuery cb, string data, 
        UserFlowState? flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        if (!data.StartsWith("goal:")) return false;

        var userId = cb.From.Id;
        var chatId = cb.Message!.Chat.Id;
        var msgId = cb.Message.MessageId;

        if (!flowDict.TryGetValue(userId, out var gFlow))
        {
            gFlow = new UserFlowState();
            flowDict[userId] = gFlow;
        }

        // === НАВИГАЦИЯ ===
        switch (data)
        {
            case "goal:main":
                gFlow.Step = UserFlowStep.None;
                await goalCmd.ShowMainAsync(bot, chatId, userId, msgId, ct);
                return true;

            case "goal:deposit":
                gFlow.Step = UserFlowStep.WaitingGoalDeposit;
                var mainDep = await goalService.GetActiveGoalAsync(userId, ct);
                gFlow.PendingGoalId = mainDep?.Id;
                await goalCmd.ShowDepositAsync(bot, chatId, userId, msgId, ct);
                return true;

            case "goal:withdraw":
                gFlow.Step = UserFlowStep.WaitingGoalWithdraw;
                var mainWd = await goalService.GetActiveGoalAsync(userId, ct);
                gFlow.PendingGoalId = mainWd?.Id;
                await goalCmd.ShowWithdrawAsync(bot, chatId, userId, msgId, ct);
                return true;

            case "goal:settings":
                gFlow.Step = UserFlowStep.None;
                await goalCmd.ShowSettingsAsync(bot, chatId, userId, msgId, ct);
                return true;

            case "goal:list":
                gFlow.Step = UserFlowStep.WaitingGoalSelect;
                gFlow.PendingListPage = 0;
                await goalCmd.ShowListAsync(bot, chatId, userId, msgId, 0, ct);
                return true;

            case "goal:create":
                gFlow.Step = UserFlowStep.WaitingGoalName;
                await bot.EditMessageTextAsync(chatId, msgId, 
                    "🎯 *Новая цель*\n\nВведите название:", 
                    Telegram.Bot.Types.Enums.ParseMode.Markdown, 
                    replyMarkup: GoalKeyboards.Cancel(), cancellationToken: ct);
                return true;

            case "goal:noop":
                return true; // Ignore click
        }

        // === ПАГИНАЦИЯ ===
        if (data.StartsWith("goal:list:"))
        {
            if (int.TryParse(data.Split(':')[2], out var page))
            {
                gFlow.PendingListPage = page;
                await goalCmd.ShowListAsync(bot, chatId, userId, msgId, page, ct);
            }
            return true;
        }

        // === ПОПОЛНЕНИЕ ===
        if (data.StartsWith("goal:add:"))
        {
            var amountStr = data.Split(':')[2];
            if (decimal.TryParse(amountStr, out var amount) && amount > 0)
            {
                gFlow.Step = UserFlowStep.None;
                await goalCmd.DepositAsync(bot, chatId, userId, amount, msgId, ct);
            }
            return true;
        }

        // === СНЯТИЕ ===
        if (data.StartsWith("goal:take:"))
        {
            var amountStr = data.Split(':')[2];
            if (decimal.TryParse(amountStr, out var amount) && amount > 0)
            {
                gFlow.Step = UserFlowStep.None;
                await goalCmd.WithdrawAsync(bot, chatId, userId, amount, msgId, ct);
            }
            return true;
        }

        // === ВЫБОР ЦЕЛИ ===
        if (data.StartsWith("goal:select:"))
        {
            if (int.TryParse(data.Split(':')[2], out var goalId))
            {
                gFlow.Step = UserFlowStep.None;
                await goalCmd.SelectGoalAsync(bot, chatId, userId, goalId, msgId, ct);
            }
            return true;
        }

        // === СДЕЛАТЬ ГЛАВНОЙ ===
        if (data.StartsWith("goal:setmain:"))
        {
            if (int.TryParse(data.Split(':')[2], out var goalId))
            {
                gFlow.Step = UserFlowStep.None;
                await goalCmd.SetMainAsync(bot, chatId, userId, goalId, msgId, ct);
            }
            return true;
        }

        // === ПОБЕДА ===
        if (data.StartsWith("goal:bought:"))
        {
            if (int.TryParse(data.Split(':')[2], out var goalId))
                await goalCmd.BoughtAsync(bot, chatId, userId, goalId, msgId, ct);
            return true;
        }

        if (data.StartsWith("goal:continue:"))
        {
            await goalCmd.ShowMainAsync(bot, chatId, userId, msgId, ct);
            return true;
        }

        // === ПЕРЕПОЛНЕНИЕ ===
        if (data.StartsWith("goal:overflow:") && !data.Contains("keep") && !data.Contains("to"))
        {
            var amountStr = data.Split(':')[2];
            if (decimal.TryParse(amountStr, out var amount))
            {
                gFlow.PendingAmount = amount;
                await goalCmd.ShowOverflowTargetsAsync(bot, chatId, userId, amount, msgId, ct);
            }
            return true;
        }

        if (data.StartsWith("goal:overflow:keep:"))
        {
            gFlow.Step = UserFlowStep.None;
            await goalCmd.ShowMainAsync(bot, chatId, userId, msgId, ct);
            return true;
        }

        if (data.StartsWith("goal:overflow:to:"))
        {
            var parts = data.Split(':');
            if (parts.Length >= 5 && int.TryParse(parts[3], out var targetId) && decimal.TryParse(parts[4], out var amount))
            {
                await goalCmd.TransferOverflowAsync(bot, chatId, userId, targetId, amount, msgId, ct);
            }
            return true;
        }

        // === УДАЛЕНИЕ ===
        if (data.StartsWith("goal:delete:") && !data.Contains("confirm"))
        {
            if (int.TryParse(data.Split(':')[2], out var goalId))
                await goalCmd.ShowDeleteConfirmAsync(bot, chatId, userId, goalId, msgId, ct);
            return true;
        }

        if (data.StartsWith("goal:delete:confirm:"))
        {
            if (int.TryParse(data.Split(':')[3], out var goalId))
                await goalCmd.DeleteGoalAsync(bot, chatId, userId, goalId, msgId, ct);
            return true;
        }

        // === РЕДАКТИРОВАНИЕ ===
        if (data.StartsWith("goal:edit:name:"))
        {
            if (int.TryParse(data.Split(':')[3], out var goalId))
            {
                gFlow.Step = UserFlowStep.WaitingGoalEditName;
                gFlow.PendingGoalId = goalId;
                var goal = await goalService.GetByIdAsync(userId, goalId, ct);
                await bot.EditMessageTextAsync(chatId, msgId, 
                    $"✏️ *Новое название*\n\nТекущее: {goal?.Name}\n\nВведите новое название:", 
                    Telegram.Bot.Types.Enums.ParseMode.Markdown, 
                    replyMarkup: GoalKeyboards.Cancel(), cancellationToken: ct);
            }
            return true;
        }

        if (data.StartsWith("goal:edit:amount:"))
        {
            if (int.TryParse(data.Split(':')[3], out var goalId))
            {
                gFlow.Step = UserFlowStep.WaitingGoalEditAmount;
                gFlow.PendingGoalId = goalId;
                var goal = await goalService.GetByIdAsync(userId, goalId, ct);
                await bot.EditMessageTextAsync(chatId, msgId, 
                    $"💵 *Новая сумма*\n\nТекущая: {goal?.TargetAmount:N0} TJS\n\nВведите новую сумму:", 
                    Telegram.Bot.Types.Enums.ParseMode.Markdown, 
                    replyMarkup: GoalKeyboards.Cancel(), cancellationToken: ct);
            }
            return true;
        }

        if (data.StartsWith("goal:edit:deadline:"))
        {
            if (int.TryParse(data.Split(':')[3], out var goalId))
            {
                gFlow.Step = UserFlowStep.WaitingGoalEditDeadline;
                gFlow.PendingGoalId = goalId;
                var goal = await goalService.GetByIdAsync(userId, goalId, ct);
                var current = goal?.Deadline.HasValue == true ? goal.Deadline.Value.ToString("dd.MM.yyyy") : "не установлен";
                await bot.EditMessageTextAsync(chatId, msgId, 
                    $"📅 *Новый дедлайн*\n\nТекущий: {current}\n\nВведите (ДД.ММ.ГГГГ) или «нет»:", 
                    Telegram.Bot.Types.Enums.ParseMode.Markdown, 
                    replyMarkup: GoalKeyboards.Cancel(), cancellationToken: ct);
            }
            return true;
        }

        return false;
    }
}
