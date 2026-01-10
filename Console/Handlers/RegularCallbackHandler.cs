using Console.Bot;
using Console.Commands;
using Console.Flow;
using Domain.Enums;
using Infrastructure.Services;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Console.Handlers;

public class RegularCallbackHandler(
    RegularPaymentCommand regularCmd,
    IRegularPaymentService regularService) : ICallbackHandler
{
    public async Task<bool> HandleAsync(ITelegramBotClient bot, CallbackQuery cb, string data, UserFlowState? flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        var userId = cb.From.Id;
        var chatId = cb.Message!.Chat.Id;
        var msgId = cb.Message.MessageId;

        // СОЗДАНИЕ СУЩНОСТЕЙ
        
        if (data == "regular:create")
        {
            flowDict[userId] = new UserFlowState { Step = UserFlowStep.WaitingRegularName };
            await bot.SendTextMessageAsync(chatId, "Введите название платежа:", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
            return true;
        }

        if (data.StartsWith("reg:freq:") && flowDict.TryGetValue(userId, out var regFlow) && regFlow.Step == UserFlowStep.WaitingRegularFrequency)
        {
            if (Enum.TryParse<PaymentFrequency>(data.Split(':')[2], out var freq))
            {
                regFlow.PendingRegularFrequency = freq;
                regFlow.Step = UserFlowStep.WaitingRegularDate;
                await bot.SendTextMessageAsync(chatId, "Введите дату (ДД.ММ.ГГГГ):", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
            }
            return true;
        }

        // === РЕГУЛЯРНЫЕ ПЛАТЕЖИ ===
        
        if (data.StartsWith("regular:pay:") && int.TryParse(data.Split(':')[2], out var payRegId))
        {
            var payment = await regularService.MarkAsPaidAsync(userId, payRegId, ct);
            if (payment != null)
                await bot.SendTextMessageAsync(chatId, $"✅ Платёж \"{payment.Name}\" оплачен!\nСледующий: {payment.NextDueDate:dd.MM.yyyy}", replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
            return true;
        }

        if (data.StartsWith("regular:pause:") && int.TryParse(data.Split(':')[2], out var pauseId))
        {
            await regularService.SetPausedAsync(userId, pauseId, true, ct);
            await regularCmd.ShowMenuAsync(bot, chatId, userId, ct);
            return true;
        }

        if (data.StartsWith("regular:resume:") && int.TryParse(data.Split(':')[2], out var resumeId))
        {
            await regularService.SetPausedAsync(userId, resumeId, false, ct);
            await regularCmd.ShowMenuAsync(bot, chatId, userId, ct);
            return true;
        }

        if (data.StartsWith("regular:delete:") && int.TryParse(data.Split(':')[2], out var delRegId))
        {
            await regularService.DeleteAsync(userId, delRegId, ct);
            await bot.SendTextMessageAsync(chatId, "✅ Платёж удалён", replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
            return true;
        }

        return false;
    }
}
