using Console.Bot;
using Console.Commands;
using Console.Flow;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Services;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Console.Handlers;

public class GoalCallbackHandler(
    GoalCommand goalCmd,
    IGoalService goalService,
    IAccountService accountService,
    ICategoryService categoryService,
    TransactionFlowHandler transactionFlowHandler) : ICallbackHandler
{
    public async Task<bool> HandleAsync(ITelegramBotClient bot, CallbackQuery cb, string data, UserFlowState? flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        if (data == "goal:create")
        {
            flowDict[cb.From.Id] = new UserFlowState { Step = UserFlowStep.WaitingGoalName };
            await bot.SendTextMessageAsync(cb.Message!.Chat.Id, "Введите название цели:", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
            return true;
        }

        if (!data.StartsWith("goal:")) return false;

        var userId = cb.From.Id;
        var chatId = cb.Message!.Chat.Id;
        var msgId = cb.Message.MessageId;

        // Инициализируем flow для навигации
        if (!flowDict.TryGetValue(userId, out var gFlow))
        {
            gFlow = new UserFlowState();
            flowDict[userId] = gFlow;
        }

        switch (data)
        {
            case "goal:main":
                gFlow.CurrentGoalScreen = GoalScreen.Main;
                await goalCmd.RenderCurrentScreenAsync(bot, chatId, userId, gFlow, ct, msgId);
                return true;
            case "goal:deposit":
                gFlow.CurrentGoalScreen = GoalScreen.Deposit;
                await goalCmd.RenderCurrentScreenAsync(bot, chatId, userId, gFlow, ct, msgId);
                return true;
            case "goal:withdraw":
                gFlow.CurrentGoalScreen = GoalScreen.Withdraw;
                await goalCmd.RenderCurrentScreenAsync(bot, chatId, userId, gFlow, ct, msgId);
                return true;
            case "goal:list":
                gFlow.CurrentGoalScreen = GoalScreen.List;
                await goalCmd.RenderCurrentScreenAsync(bot, chatId, userId, gFlow, ct, msgId);
                return true;
            case "goal:settings":
                gFlow.CurrentGoalScreen = GoalScreen.Settings;
                await goalCmd.RenderCurrentScreenAsync(bot, chatId, userId, gFlow, ct, msgId);
                return true;
            case "goal:transfer:yes":
                await goalCmd.HandleTransferAsync(bot, chatId, userId, gFlow, true, ct, msgId);
                return true;
            case "goal:transfer:no":
                await goalCmd.HandleTransferAsync(bot, chatId, userId, gFlow, false, ct, msgId);
                return true;
        }

        // goal:add:100, goal:add:500, goal:add:1000, goal:add:all
        if (data.StartsWith("goal:add:"))
        {
            var amountStr = data.Split(':')[2];
            decimal amount;
            if (amountStr == "all")
            {
                var acc = await accountService.GetUserAccountAsync(userId, ct);
                amount = acc?.Balance ?? 0;
            }
            else
            {
                decimal.TryParse(amountStr, out amount);
            }
            if (amount > 0)
                await goalCmd.HandleDepositAsync(bot, chatId, userId, gFlow, amount, ct, msgId);
            return true;
        }

        // goal:take:100, goal:take:500, goal:take:1000, goal:take:all
        if (data.StartsWith("goal:take:"))
        {
            var amountStr = data.Split(':')[2];
            decimal amount;
            if (amountStr == "all")
            {
                var main = await goalService.GetActiveGoalAsync(userId, ct);
                amount = main?.CurrentAmount ?? 0;
            }
            else
            {
                decimal.TryParse(amountStr, out amount);
            }
            if (amount > 0)
                await goalCmd.HandleWithdrawAsync(bot, chatId, userId, gFlow, amount, ct, msgId);
            return true;
        }

        // goal:select:123 — выбор новой главной цели
        if (data.StartsWith("goal:select:"))
        {
            if (int.TryParse(data.Split(':')[2], out var newGoalId))
                await goalCmd.HandleSelectGoalAsync(bot, chatId, userId, gFlow, newGoalId, ct, msgId);
            return true;
        }

        // goal:bought:123 — цель достигнута, пользователь купил
        if (data.StartsWith("goal:bought:"))
        {
            if (int.TryParse(data.Split(':')[2], out var boughtGoalId))
            {
                var goal = await goalService.GetByIdAsync(userId, boughtGoalId, ct);
                if (goal != null)
                {
                    // Создаем расход
                    var cat = (await categoryService.GetUserCategoriesAsync(userId, ct))
                        .FirstOrDefault(c => c.Type == TransactionType.Expense);
                    if (cat != null)
                        await transactionFlowHandler.AddTransactionWithDescriptionAsync(bot, chatId, userId, goal.CurrentAmount, cat.Id, TransactionType.Expense, $"Покупка: {goal.Name}", false, ct);
                    
                    // Завершаем цель
                    await goalService.CompleteAsync(userId, boughtGoalId, ct);
                }
                gFlow.CurrentGoalScreen = GoalScreen.Main;
                await goalCmd.RenderCurrentScreenAsync(bot, chatId, userId, gFlow, ct, msgId);
            }
            return true;
        }

        // goal:delete:123 — удаление цели
        if (data.StartsWith("goal:delete:"))
        {
            if (int.TryParse(data.Split(':')[2], out var delGoalId))
            {
                await goalService.DeleteAsync(userId, delGoalId, ct);
                gFlow.CurrentGoalScreen = GoalScreen.Main;
                await goalCmd.RenderCurrentScreenAsync(bot, chatId, userId, gFlow, ct, msgId);
            }
            return true;
        }

        return false;
    }
}
