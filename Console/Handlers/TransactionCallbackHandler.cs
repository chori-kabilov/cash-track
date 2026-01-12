using Console.Bot;
using Console.Bot.Keyboards;
using Console.Commands;
using Console.Flow;
using Domain.Enums;
using Infrastructure.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Console.Handlers;

/// <summary>
/// Обработчик callback-кнопок для транзакций
/// </summary>
public class TransactionCallbackHandler(
    TransactionFlowHandler transactionFlowHandler,
    ITransactionService transactionService,
    ICategoryService categoryService,
    IAccountService accountService) : ICallbackHandler
{
    public async Task<bool> HandleAsync(ITelegramBotClient bot, CallbackQuery cb, string data, UserFlowState? flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        var userId = cb.From.Id;
        var chatId = cb.Message!.Chat.Id;
        var msgId = cb.Message.MessageId;

        // === СОЗДАНИЕ НОВОЙ КАТЕГОРИИ ===
        if (data == "cat:new" && flowDict.TryGetValue(userId, out var newCatFlow))
        {
            newCatFlow.Step = UserFlowStep.WaitingNewCategory;
            newCatFlow.PendingMessageId = msgId;
            await transactionFlowHandler.ShowNewCategoryPromptAsync(bot, chatId, msgId, newCatFlow.PendingType, ct);
            return true;
        }

        // === NOOP (для неактивных кнопок пагинации) ===
        if (data == "cat:noop") return true;

        // === ПАГИНАЦИЯ КАТЕГОРИЙ ===
        if (data.StartsWith("cat:page:") && flowDict.TryGetValue(userId, out var pageFlow))
        {
            var pageParts = data.Split(':');
            if (pageParts.Length == 3 && int.TryParse(pageParts[2], out var page))
            {
                var (top2, others) = await transactionFlowHandler.GetCategoriesAsync(userId, pageFlow.PendingType, ct);
                var typeEmoji = pageFlow.PendingType == TransactionType.Income ? "💰" : "💸";
                var typeLabel = pageFlow.PendingType == TransactionType.Income ? "Записываем доход" : "Записываем расход";
                var prompt = $"{typeEmoji} *{typeLabel}*\n\n" +
                             $"💵 Сумма: *{pageFlow.PendingAmount:N0} TJS*\n\n" +
                             $"Выберите категорию или создайте новую:";
                
                var keyboard = TransactionKeyboards.SmartCategories(top2, others, pageFlow.PendingType, page);
                await CommandHelpers.SafeEditMessageAsync(bot, chatId, msgId, prompt, keyboard, ct, cb.Id);
                return true;
            }
        }

        // === ВЫБОР СУЩЕСТВУЮЩЕЙ КАТЕГОРИИ ===
        if (data.StartsWith("cat:") && !data.StartsWith("cat:new") && !data.StartsWith("cat:page") && !data.StartsWith("cat:noop"))
        {
            var parts = data.Split(':');
            if (parts.Length == 3 && int.TryParse(parts[2], out var catId) && flowDict.TryGetValue(userId, out var catFlow) && catFlow.Step == UserFlowStep.ChoosingCategory)
            {
                catFlow.PendingCategoryId = catId;
                catFlow.PendingMessageId = msgId;
                
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
        }
        
        // === НАВИГАЦИЯ "НАЗАД" ===
        
        // Назад к вводу суммы (из категорий)
        if (data == "back:amount" && flowDict.TryGetValue(userId, out var backAmountFlow))
        {
            backAmountFlow.Step = UserFlowStep.WaitingAmount;
            backAmountFlow.PendingMessageId = msgId;
            
            var emoji = backAmountFlow.PendingType == TransactionType.Expense ? "💸" : "💰";
            var typeLabel = backAmountFlow.PendingType == TransactionType.Expense ? "Расход" : "Доход";
            var keyboard = backAmountFlow.PendingType == TransactionType.Expense 
                ? TransactionKeyboards.ExpenseStart(backAmountFlow.PendingIsImpulsive) 
                : TransactionKeyboards.IncomeStart();
            
            await CommandHelpers.SafeEditMessageAsync(bot, chatId, msgId,
                $"{emoji} *{typeLabel}*\n\nВведите сумму:\n_Можно добавить описание через пробел_",
                keyboard, ct, cb.Id);
            return true;
        }
        
        // Назад к категориям (из ввода новой категории)
        if (data == "back:categories" && flowDict.TryGetValue(userId, out var backCatFlow))
        {
            backCatFlow.Step = UserFlowStep.ChoosingCategory;
            var (top2, others) = await transactionFlowHandler.GetCategoriesAsync(userId, backCatFlow.PendingType, ct);
            
            var typeEmoji = backCatFlow.PendingType == TransactionType.Income ? "💰" : "💸";
            var typeLabel = backCatFlow.PendingType == TransactionType.Income ? "Записываем доход" : "Записываем расход";
            var prompt = $"{typeEmoji} *{typeLabel}*\n\n" +
                         $"💵 Сумма: *{backCatFlow.PendingAmount:N0} TJS*\n\n" +
                         $"Выберите категорию или создайте новую:";
            
            await CommandHelpers.SafeEditMessageAsync(bot, chatId, msgId, prompt,
                TransactionKeyboards.SmartCategories(top2, others, backCatFlow.PendingType), ct, cb.Id);
            return true;
        }
        
        // === ГОТОВО ===
        if (data == "txn:done")
        {
            if (flowDict.TryGetValue(userId, out var doneFlow))
            {
                var type = doneFlow.PendingType;
                var amount = doneFlow.PendingAmount;
                var catId = doneFlow.PendingCategoryId;
                var description = doneFlow.PendingDescription;
                
                // Получаем категорию и баланс
                string? catName = null;
                string? catIcon = null;
                if (catId.HasValue)
                {
                    var cat = await categoryService.GetCategoryByIdAsync(userId, catId.Value, ct);
                    catName = cat?.Name;
                    catIcon = cat?.Icon;
                }
                var account = await accountService.GetUserAccountAsync(userId, ct);
                var balance = account?.Balance ?? 0;
                
                flowDict.Remove(userId);
                await transactionFlowHandler.ShowSuccessAsync(bot, chatId, msgId, type, amount, catName, catIcon, description, balance, ct);
            }
            else
            {
                await CommandHelpers.SafeEditMessageAsync(bot, chatId, msgId, "✅ *Готово!*\n\nЧто дальше?", 
                    TransactionKeyboards.AfterTransaction(), ct, cb.Id);
            }
            return true;
        }
        
        // === ОТМЕНА ТРАНЗАКЦИИ ===
        if (data == "txn:cancel")
        {
            if (flowDict.TryGetValue(userId, out var cancelFlow) && cancelFlow.PendingTransactionId.HasValue)
            {
                await transactionService.DeleteAsync(cancelFlow.PendingTransactionId.Value, ct);
            }
            flowDict.Remove(userId);
            await transactionFlowHandler.ShowCancelledAsync(bot, chatId, msgId, ct);
            return true;
        }

        return false;
    }
}
