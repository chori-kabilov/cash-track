using Console.Bot;
using Console.Commands;
using Console.Flow;
using Domain.Enums;
using Infrastructure.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace Console.Handlers;

public class BalanceCallbackHandler(
    BalanceCommand balanceCmd,
    ITransactionService transactionService) : ICallbackHandler
{
    public async Task<bool> HandleAsync(ITelegramBotClient bot, CallbackQuery cb, string data, UserFlowState? flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        var userId = cb.From.Id;
        var chatId = cb.Message!.Chat.Id;
        var msgId = cb.Message.MessageId;

        // === БАЛАНС: ПЕРЕКЛЮЧАТЕЛИ ===
        
        if (data.StartsWith("bal:toggle_"))
        {
            // Инициализируем flow если его нет
            if (!flowDict.TryGetValue(userId, out var toggleFlow))
            {
                toggleFlow = new UserFlowState();
                flowDict[userId] = toggleFlow;
            }

            switch (data)
            {
                case "bal:toggle_debts": toggleFlow.BalanceShowDebts = !toggleFlow.BalanceShowDebts; break;
                case "bal:toggle_goals": toggleFlow.BalanceShowGoals = !toggleFlow.BalanceShowGoals; break;
                case "bal:toggle_payments": toggleFlow.BalanceShowPayments = !toggleFlow.BalanceShowPayments; break;
            }
            await balanceCmd.ExecuteAsync(bot, chatId, userId, toggleFlow, ct, msgId);
            return true;
        }
        
        if (data == "bal:back")
        {
            flowDict.Remove(userId);
            await bot.EditMessageTextAsync(chatId, msgId, "🏠 *Главное меню*\n\nВыберите действие:", 
                replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
            return true;
        }
        
        if (data == "bal:details")
        {
            var transactions = await transactionService.GetUserTransactionsAsync(userId, 10, ct);
            var lines = transactions.Select(t => 
                $"{(t.Type == TransactionType.Income ? "+" : "-")}{t.Amount:F0} {t.Category?.Icon} {t.Description ?? t.Category?.Name}");
            var text = "📊 *Последние операции:*\n\n" + string.Join("\n", lines);
            
            // Только кнопка "Назад" к балансу
            await bot.EditMessageTextAsync(chatId, msgId, text,
                ParseMode.Markdown, replyMarkup: BotInlineKeyboards.BalanceDetails(), cancellationToken: ct);
            return true;
        }
        
        if (data == "bal:back_to_dashboard")
        {
            if (!flowDict.TryGetValue(userId, out var backDashFlow))
            {
                backDashFlow = new UserFlowState();
                flowDict[userId] = backDashFlow;
            }
            await balanceCmd.ExecuteAsync(bot, chatId, userId, backDashFlow, ct, msgId);
            return true;
        }

        return false;
    }
}
