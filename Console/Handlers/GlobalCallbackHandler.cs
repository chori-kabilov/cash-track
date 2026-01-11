using Console.Bot;
using Console.Bot.Keyboards;
using Console.Flow;
using Domain.Enums;
using Infrastructure.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Console.Handlers;

// Глобальный обработчик callback-кнопок (отмена, эмоции и т.д.)
public class GlobalCallbackHandler(
    TransactionFlowHandler transactionFlowHandler,
    ITransactionService transactionService) : ICallbackHandler
{
    public async Task<bool> HandleAsync(ITelegramBotClient bot, CallbackQuery cb, string data, UserFlowState? flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        var userId = cb.From.Id;
        var chatId = cb.Message!.Chat.Id;
        var msgId = cb.Message.MessageId;

        // === ГЛОБАЛЬНАЯ ОТМЕНА (РЕДАКТИРУЕТ СООБЩЕНИЕ) ===
        if (data == "action:cancel:edit")
        {
            flowDict.Remove(userId);
            await bot.EditMessageTextAsync(chatId, msgId, "🏠 *Главное меню*\n\nВыберите действие:", 
                ParseMode.Markdown, replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
            return true;
        }

        // === ГЛОБАЛЬНАЯ ОТМЕНА (СТАРЫЙ, для совместимости — тоже редактирует) ===
        if (data == "action:cancel")
        {
            flowDict.Remove(userId);
            try
            {
                await bot.EditMessageTextAsync(chatId, msgId, "🏠 *Главное меню*\n\nВыберите действие:", 
                    ParseMode.Markdown, replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
            }
            catch
            {
                await bot.SendTextMessageAsync(chatId, "🏠 *Главное меню*\n\nВыберите действие:", 
                    replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
            }
            return true;
        }

        // === ПРОПУСТИТЬ ОПИСАНИЕ ===
        if (data == "action:skip_desc" && flowDict.TryGetValue(userId, out var skipFlow) && skipFlow.Step == UserFlowStep.WaitingDescription)
        {
            await transactionFlowHandler.AddTransactionWithDescriptionAsync(bot, chatId, userId, 
                skipFlow.PendingAmount, skipFlow.PendingCategoryId!.Value, skipFlow.PendingType, null, skipFlow.PendingIsImpulsive, ct);
            flowDict.Remove(userId);
            return true;
        }

        // === ПЕРЕКЛЮЧЕНИЕ "НА ЭМОЦИЯХ" ===
        if (data == "action:toggle_impulsive" && flowDict.TryGetValue(userId, out var impFlow) && 
            impFlow.Step == UserFlowStep.WaitingAmount && impFlow.PendingType == TransactionType.Expense)
        {
            impFlow.PendingIsImpulsive = !impFlow.PendingIsImpulsive;
            await bot.EditMessageReplyMarkupAsync(chatId, msgId, 
                replyMarkup: TransactionKeyboards.ExpenseStart(impFlow.PendingIsImpulsive), cancellationToken: ct);
            return true;
        }
        
        // === РЕТРАЙ (повторить после отмены) ===
        if (data == "menu:retry")
        {
            await bot.EditMessageTextAsync(chatId, msgId, "💵 Выберите тип операции:", 
                ParseMode.Markdown, replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
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
                await bot.EditMessageTextAsync(chatId, msgId, 
                    $"✅ *Транзакция отменена*\n\n{sign}{lastTx.Amount:N0} TJS — {lastTx.Category?.Name}", 
                    ParseMode.Markdown, replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
            }
            else
            {
                await bot.EditMessageTextAsync(chatId, msgId, 
                    "❌ *Нет транзакций для отмены*", 
                    ParseMode.Markdown, replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
            }
            return true;
        }

        return false;
    }
}
