using Console.Commands;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Console.Flow;

// Обработчик текстового ввода для Помощи (баги, идеи)
public class HelpFlowHandler(HelpCommand helpCmd) : IFlowStepHandler
{
    private static readonly UserFlowStep[] HandledSteps =
    {
        UserFlowStep.WaitingHelpBug,
        UserFlowStep.WaitingHelpIdea
    };

    public bool CanHandle(UserFlowStep step) => HandledSteps.Contains(step);

    public async Task<bool> HandleAsync(ITelegramBotClient bot, long chatId, long userId, string text,
        UserFlowState flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        // Базовый вызов без данных пользователя
        return await HandleWithUserAsync(bot, chatId, userId, null, null, null, text, flow, flowDict, ct);
    }

    // Расширенный вызов с данными пользователя
    public async Task<bool> HandleWithUserAsync(ITelegramBotClient bot, long chatId, long userId,
        string? firstName, string? lastName, string? username, string text,
        UserFlowState flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        var type = flow.Step == UserFlowStep.WaitingHelpBug ? "bug" : "idea";
        await helpCmd.SendFeedbackAsync(bot, chatId, userId, firstName, lastName, username, text.Trim(), type, ct);
        flowDict.Remove(userId);
        return true;
    }
}
