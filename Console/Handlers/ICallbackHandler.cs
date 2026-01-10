using Console.Flow;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Console.Handlers;

public interface ICallbackHandler
{
    Task<bool> HandleAsync(ITelegramBotClient bot, CallbackQuery cb, string data, UserFlowState? flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct);
}
