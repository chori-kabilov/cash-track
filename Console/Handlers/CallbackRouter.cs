using Console.Flow;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Console.Handlers;

// Роутер callback-запросов (вызывает обработчики по порядку)
public class CallbackRouter(IEnumerable<ICallbackHandler> handlers)
{
    public async Task RouteAsync(ITelegramBotClient bot, CallbackQuery cb, UserFlowState? flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        var data = cb.Data ?? "";
        if (string.IsNullOrEmpty(data)) return;

        foreach (var handler in handlers)
        {
            if (await handler.HandleAsync(bot, cb, data, flow, flowDict, ct))
                return;
        }
    }
}
