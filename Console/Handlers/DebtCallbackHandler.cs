using Console.Bot;
using Console.Flow;
using Domain.Enums;
using Infrastructure.Services;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Console.Handlers;

public class DebtCallbackHandler(
    IDebtService debtService) : ICallbackHandler
{
    public async Task<bool> HandleAsync(ITelegramBotClient bot, CallbackQuery cb, string data, UserFlowState? flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        var userId = cb.From.Id;
        var chatId = cb.Message!.Chat.Id;

        // === ДОЛГИ: СОЗДАНИЕ ===
        
        if (data.StartsWith("debt:create:"))
        {
            var type = data.Split(':')[2] == "i_owe" ? DebtType.IOwe : DebtType.TheyOwe;
            flowDict[userId] = new UserFlowState { Step = UserFlowStep.WaitingDebtName, PendingDebtType = type };
            await bot.SendTextMessageAsync(chatId, type == DebtType.IOwe ? "Кому вы должны?" : "Кто вам должен?", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
            return true;
        }

        // === ДОЛГИ: УПРАВЛЕНИЕ ===
        
        if (data.StartsWith("debt:pay:") && int.TryParse(data.Split(':')[2], out var payDebtId))
        {
            flowDict[userId] = new UserFlowState { Step = UserFlowStep.WaitingDebtPayment, PendingDebtId = payDebtId };
            await bot.SendTextMessageAsync(chatId, "Введите сумму платежа:", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
            return true;
        }

        if (data.StartsWith("debt:close:") && int.TryParse(data.Split(':')[2], out var closeDebtId))
        {
            await debtService.MarkAsPaidAsync(userId, closeDebtId, ct);
            await bot.SendTextMessageAsync(chatId, "✅ Долг закрыт!", replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
            return true;
        }

        if (data.StartsWith("debt:delete:") && int.TryParse(data.Split(':')[2], out var delDebtId))
        {
            await debtService.DeleteAsync(userId, delDebtId, ct);
            await bot.SendTextMessageAsync(chatId, "✅ Долг удалён", replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
            return true;
        }

        return false;
    }
}
