using Console.Bot;
using Console.Flow;
using Domain.Enums;
using Infrastructure.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Console.Handlers;

public class GlobalCallbackHandler(
    TransactionFlowHandler transactionFlowHandler,
    ITransactionService transactionService) : ICallbackHandler
{
    public async Task<bool> HandleAsync(ITelegramBotClient bot, CallbackQuery cb, string data, UserFlowState? flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        var userId = cb.From.Id;
        var chatId = cb.Message!.Chat.Id;
        var msgId = cb.Message.MessageId;

        // ГЛОБАЛЬНЫЕ ДЕЙСТВИЯ
        
        if (data == "action:cancel")
        {
            flowDict.Remove(userId);
            await bot.SendTextMessageAsync(chatId, "Выберите действие:", replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
            return true;
        }

        if (data == "action:skip_desc" && flowDict.TryGetValue(userId, out var skipFlow) && skipFlow.Step == UserFlowStep.WaitingDescription)
        {
            await transactionFlowHandler.AddTransactionWithDescriptionAsync(bot, chatId, userId, skipFlow.PendingAmount, skipFlow.PendingCategoryId!.Value, skipFlow.PendingType, null, skipFlow.PendingIsImpulsive, ct);
            flowDict.Remove(userId);
            return true;
        }

        // Переключение флага "На эмоциях" для расхода
        if (data == "action:toggle_impulsive" && flowDict.TryGetValue(userId, out var impFlow) && impFlow.Step == UserFlowStep.WaitingAmount && impFlow.PendingType == TransactionType.Expense)
        {
            impFlow.PendingIsImpulsive = !impFlow.PendingIsImpulsive;
            await bot.EditMessageReplyMarkupAsync(chatId, msgId, replyMarkup: BotInlineKeyboards.ExpenseStart(impFlow.PendingIsImpulsive), cancellationToken: ct);
            return true;
        }
        
        // === ОТМЕНА ПОСЛЕДНЕЙ ТРАНЗАКЦИИ ===
        
        if (data == "action:cancel_last_tx")
        {
            var lastTx = await transactionService.GetLastTransactionAsync(userId, ct);
            if (lastTx != null && !lastTx.IsError)
            {
                await transactionService.CancelAsync(lastTx.Id, ct);
                var sign = lastTx.Type == TransactionType.Income ? "+" : "-";
                await bot.SendTextMessageAsync(chatId, $"✅ Транзакция отменена\n{sign}{lastTx.Amount:F2} — {lastTx.Category?.Name}", replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
            }
            else
            {
                await bot.SendTextMessageAsync(chatId, "❌ Нет транзакций для отмены", replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
            }
            return true;
        }

        return false;
    }
}
