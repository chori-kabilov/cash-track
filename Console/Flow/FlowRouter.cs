using Telegram.Bot;

namespace Console.Flow;

// Роутер диалоговых шагов (вызывает нужный handler)
public class FlowRouter(IEnumerable<IFlowStepHandler> handlers)
{
    public async Task<bool> HandleAsync(
        ITelegramBotClient bot, 
        long chatId, 
        long userId, 
        string text, 
        UserFlowState flow, 
        Dictionary<long, UserFlowState> flowDict,
        CancellationToken ct)
    {
        foreach (var handler in handlers)
        {
            if (handler.CanHandle(flow.Step))
                return await handler.HandleAsync(bot, chatId, userId, text, flow, flowDict, ct);
        }
        return false;
    }
}
