using Console.Bot;
using Console.Flow;
using Domain.Enums;
using Infrastructure.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Console.Handlers;

public class TransactionCallbackHandler(
    TransactionFlowHandler transactionFlowHandler,
    ITransactionService transactionService) : ICallbackHandler
{
    public async Task<bool> HandleAsync(ITelegramBotClient bot, CallbackQuery cb, string data, UserFlowState? flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        var userId = cb.From.Id;
        var chatId = cb.Message!.Chat.Id;
        var msgId = cb.Message.MessageId;

        // "Другое" — редактируем сообщение для ввода названия
        if (data == "cat:new" && flowDict.TryGetValue(userId, out var newCatFlow))
        {
            newCatFlow.Step = UserFlowStep.WaitingNewCategory;
            await bot.EditMessageTextAsync(chatId, msgId, 
                "🆕 *Новый источник?*\n\nВведите название:", 
                ParseMode.Markdown, replyMarkup: BotInlineKeyboards.NewCategoryInput(), cancellationToken: ct);
            return true;
        }

        // Выбор существующей категории — сразу записываем транзакцию
        if (data.StartsWith("cat:"))
        {
            var parts = data.Split(':');
            if (parts.Length == 3 && int.TryParse(parts[2], out var catId) && flowDict.TryGetValue(userId, out var catFlow) && catFlow.Step == UserFlowStep.ChoosingCategory)
            {
                catFlow.PendingCategoryId = catId;
                
                // Записываем транзакцию и показываем результат
                var (txnId, resultMsgId) = await transactionFlowHandler.AddTransactionAsync(bot, chatId, userId, catFlow, ct);
                if (txnId.HasValue)
                {
                    catFlow.PendingTransactionId = txnId;
                    catFlow.PendingMessageId = resultMsgId;
                    catFlow.Step = UserFlowStep.None;
                }
                else
                {
                    flowDict.Remove(userId);
                }
                return true;
            }
            // Если условия не совпали — показываем меню
            await bot.SendTextMessageAsync(chatId, "Выберите действие:", replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
            return true;
        }
        
        // === НАВИГАЦИЯ "НАЗАД" ===
        
        // Назад к вводу суммы (из категорий)
        if (data == "back:amount" && flowDict.TryGetValue(userId, out var backAmountFlow))
        {
            backAmountFlow.Step = UserFlowStep.WaitingAmount;
            var keyboard = backAmountFlow.PendingType == TransactionType.Expense 
                ? BotInlineKeyboards.ExpenseStart(backAmountFlow.PendingIsImpulsive) 
                : BotInlineKeyboards.Cancel();
            var emoji = backAmountFlow.PendingType == TransactionType.Expense ? "💸" : "💵";
            var typeName = backAmountFlow.PendingType == TransactionType.Expense ? "Расход" : "Доход";
            
            await bot.EditMessageTextAsync(chatId, msgId,
                $"{emoji} *{typeName}*\n\nВведите сумму и описание через пробел:",
                ParseMode.Markdown, replyMarkup: keyboard, cancellationToken: ct);
            return true;
        }
        
        // Назад к категориям (из ввода новой категории)
        if (data == "back:categories" && flowDict.TryGetValue(userId, out var backCatFlow))
        {
            backCatFlow.Step = UserFlowStep.ChoosingCategory;
            var categories = await transactionFlowHandler.GetSuggestedCategoriesAsync(userId, backCatFlow.PendingType, ct);
            var prompt = backCatFlow.PendingType == TransactionType.Income ? "Откуда доход?" : "Выберите категорию:";
            
            await bot.EditMessageTextAsync(chatId, msgId, prompt,
                replyMarkup: BotInlineKeyboards.CategoriesWithBack(categories, backCatFlow.PendingType), cancellationToken: ct);
            return true;
        }
        
        // Готово — редактируем сообщение на главное меню
        if (data == "txn:done" && flowDict.TryGetValue(userId, out var doneFlow))
        {
            flowDict.Remove(userId);
            await bot.EditMessageTextAsync(chatId, msgId, "Выберите действие:", 
                replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
            return true;
        }
        
        // Отмена транзакции — удаляем и редактируем на главное меню
        if (data == "txn:cancel")
        {
            if (flowDict.TryGetValue(userId, out var cancelFlow) && cancelFlow.PendingTransactionId.HasValue)
            {
                await transactionService.DeleteAsync(cancelFlow.PendingTransactionId.Value, ct);
                flowDict.Remove(userId);
                await bot.EditMessageTextAsync(chatId, msgId, "❌ Транзакция отменена.\n\nВыберите действие:", 
                    replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
                return true;
            }
        }

        return false;
    }
}
