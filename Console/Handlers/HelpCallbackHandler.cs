using Console.Bot.Keyboards;
using Console.Commands;
using Console.Flow;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Console.Handlers;

// Обработчик callback-кнопок для Помощи
public class HelpCallbackHandler(HelpCommand helpCmd) : ICallbackHandler
{
    public async Task<bool> HandleAsync(ITelegramBotClient bot, CallbackQuery cb, string data,
        UserFlowState? flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        if (!data.StartsWith("help:")) return false;

        var userId = cb.From.Id;
        var chatId = cb.Message!.Chat.Id;
        var msgId = cb.Message.MessageId;

        if (!flowDict.TryGetValue(userId, out var hFlow))
        {
            hFlow = new UserFlowState();
            flowDict[userId] = hFlow;
        }

        switch (data)
        {
            case "help:main":
                hFlow.Step = UserFlowStep.None;
                await helpCmd.ShowMainAsync(bot, chatId, msgId, ct);
                return true;

            case "help:guide":
                hFlow.Step = UserFlowStep.None;
                await helpCmd.ShowGuideAsync(bot, chatId, msgId, ct);
                return true;

            case "help:guide:balance":
                await helpCmd.ShowGuideBalanceAsync(bot, chatId, msgId, ct);
                return true;

            case "help:guide:stats":
                await helpCmd.ShowGuideStatsAsync(bot, chatId, msgId, ct);
                return true;

            case "help:guide:goals":
                await helpCmd.ShowGuideGoalsAsync(bot, chatId, msgId, ct);
                return true;

            case "help:guide:debts":
                await helpCmd.ShowGuideDebtsAsync(bot, chatId, msgId, ct);
                return true;

            case "help:guide:regular":
                await helpCmd.ShowGuideRegularAsync(bot, chatId, msgId, ct);
                return true;

            case "help:contact":
                await helpCmd.ShowContactAsync(bot, chatId, msgId, ct);
                return true;

            case "help:bug":
                hFlow.Step = UserFlowStep.WaitingHelpBug;
                await helpCmd.PromptBugReportAsync(bot, chatId, msgId, ct);
                return true;

            case "help:idea":
                hFlow.Step = UserFlowStep.WaitingHelpIdea;
                await helpCmd.PromptIdeaAsync(bot, chatId, msgId, ct);
                return true;
        }

        return false;
    }
}
