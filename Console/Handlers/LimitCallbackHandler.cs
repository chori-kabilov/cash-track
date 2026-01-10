using Console.Bot;
using Console.Commands;
using Console.Flow;
using Infrastructure.Services;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Console.Handlers;

public class LimitCallbackHandler(
    LimitCommand limitCmd,
    LimitService limitService) : ICallbackHandler
{
    public async Task<bool> HandleAsync(ITelegramBotClient bot, CallbackQuery cb, string data, UserFlowState? flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        var userId = cb.From.Id;
        var chatId = cb.Message!.Chat.Id;
        var msgId = cb.Message.MessageId;

        // ЛИМИТЫ
        
        if (data == "limit:create")
        {
            await limitCmd.ShowCategoriesAsync(bot, chatId, userId, ct);
            flowDict[userId] = new UserFlowState { Step = UserFlowStep.WaitingLimitCategory };
            return true;
        }

        if (data == "limit:reset")
        {
            await limitService.ResetMonthlyLimitsAsync(userId, ct);
            await bot.SendTextMessageAsync(chatId, "✅ Месячные лимиты сброшены!", replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
            return true;
        }

        if (data.StartsWith("limit:delete:") && int.TryParse(data.Split(':')[2], out var delLimitId))
        {
            await limitService.DeleteAsync(userId, delLimitId, ct);
            await limitCmd.ShowMenuAsync(bot, chatId, userId, ct);
            return true;
        }

        if (data.StartsWith("limit:cat:") && int.TryParse(data.Split(':')[2], out var limitCatId))
        {
            flowDict[userId] = new UserFlowState { Step = UserFlowStep.WaitingLimitAmount, PendingLimitCategoryId = limitCatId };
            await bot.SendTextMessageAsync(chatId, "Введите сумму лимита:", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
            return true;
        }

        return false;
    }
}
