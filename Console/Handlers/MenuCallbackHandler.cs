using Console.Bot;
using Console.Commands;
using Console.Flow;
using Domain.Enums;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Console.Handlers;

// Обработчик callback-запросов главного меню
public class MenuCallbackHandler(
    BalanceCommand balanceCmd,
    StatsCommand statsCmd,
    GoalCommand goalCmd,
    DebtCommand debtCmd,
    RegularPaymentCommand regularCmd,
    LimitCommand limitCmd,
    HelpCommand helpCmd) : ICallbackHandler
{
    public async Task<bool> HandleAsync(ITelegramBotClient bot, CallbackQuery cb, string data, UserFlowState? flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        if (!data.StartsWith("menu:")) return false;

        var userId = cb.From.Id;
        var chatId = cb.Message!.Chat.Id;
        var msgId = cb.Message.MessageId;

        switch (data)
        {
            case "menu:balance":
                if (flow == null) { flow = new UserFlowState(); flowDict[userId] = flow; }
                await balanceCmd.ExecuteAsync(bot, chatId, userId, flow, ct, msgId);
                return true;

            case "menu:stats":
                if (flow == null) { flow = new UserFlowState(); flowDict[userId] = flow; }
                await statsCmd.ExecuteAsync(bot, chatId, userId, flow, ct, msgId);
                return true;

            case "menu:help":
                await helpCmd.ExecuteAsync(bot, chatId, ct, msgId);
                return true;

            case "menu:goals":
                if (flow == null) { flow = new UserFlowState(); flowDict[userId] = flow; }
                await goalCmd.ExecuteAsync(bot, chatId, userId, flow, ct, msgId);
                return true;

            case "menu:debts":
                await debtCmd.ExecuteAsync(bot, chatId, userId, ct, msgId);
                return true;

            case "menu:regular":
                await regularCmd.ExecuteAsync(bot, chatId, userId, ct, msgId);
                return true;

            case "menu:limits":
                await limitCmd.ShowMenuAsync(bot, chatId, userId, ct, msgId);
                return true;

            case "menu:income":
                flowDict[userId] = new UserFlowState { Step = UserFlowStep.WaitingAmount, PendingType = TransactionType.Income, PendingMessageId = msgId };
                await bot.EditMessageTextAsync(chatId, msgId, 
                    "💵 *Доход*\n\nВведите сумму и описание через пробел:\n_Пример: 5000 премия_", 
                    ParseMode.Markdown, replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
                return true;

            case "menu:expense":
                flowDict[userId] = new UserFlowState { Step = UserFlowStep.WaitingAmount, PendingType = TransactionType.Expense, PendingMessageId = msgId, PendingIsImpulsive = false };
                await bot.EditMessageTextAsync(chatId, msgId, 
                    "💸 *Расход*\n\nВведите сумму и описание через пробел:\n_Пример: 150 такси_", 
                    ParseMode.Markdown, replyMarkup: BotInlineKeyboards.ExpenseStart(false), cancellationToken: ct);
                return true;

            case "menu:main":
                await bot.EditMessageTextAsync(chatId, msgId, "Выберите действие:", 
                    replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
                return true;
        }

        return false;
    }
}
