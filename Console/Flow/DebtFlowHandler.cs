using System.Globalization;
using Console.Bot;
using Domain.Enums;
using Infrastructure.Services;
using Telegram.Bot;

namespace Console.Flow;

// Обработчик шагов создания и оплаты долгов
public class DebtFlowHandler(
    IDebtService debtService,
    ICategoryService categoryService,
    TransactionFlowHandler transactionHandler) : IFlowStepHandler
{
    private static readonly UserFlowStep[] HandledSteps = 
    {
        UserFlowStep.WaitingDebtName,
        UserFlowStep.WaitingDebtAmount,
        UserFlowStep.WaitingDebtDeadline,
        UserFlowStep.WaitingDebtPayment
    };

    public bool CanHandle(UserFlowStep step) => HandledSteps.Contains(step);

    public async Task<bool> HandleAsync(ITelegramBotClient bot, long chatId, long userId, string text, 
        UserFlowState flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        return flow.Step switch
        {
            UserFlowStep.WaitingDebtName => await HandleDebtNameAsync(bot, chatId, text, flow, ct),
            UserFlowStep.WaitingDebtAmount => await HandleDebtAmountAsync(bot, chatId, text, flow, ct),
            UserFlowStep.WaitingDebtDeadline => await HandleDebtDeadlineAsync(bot, chatId, userId, text, flow, flowDict, ct),
            UserFlowStep.WaitingDebtPayment => await HandleDebtPaymentAsync(bot, chatId, userId, text, flow, flowDict, ct),
            _ => false
        };
    }

    // Шаг 1: Ввод имени должника
    private async Task<bool> HandleDebtNameAsync(ITelegramBotClient bot, long chatId, string text, UserFlowState flow, CancellationToken ct)
    {
        flow.PendingDebtName = text;
        flow.Step = UserFlowStep.WaitingDebtAmount;
        await bot.SendTextMessageAsync(chatId, "Введите сумму долга:", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
        return true;
    }

    // Шаг 2: Ввод суммы долга
    private async Task<bool> HandleDebtAmountAsync(ITelegramBotClient bot, long chatId, string text, UserFlowState flow, CancellationToken ct)
    {
        if (!FlowHelper.TryParseAmount(text, out var debtAmount) || debtAmount <= 0)
        {
            await bot.SendTextMessageAsync(chatId, "❌ Неверная сумма.", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
            return true;
        }
        flow.PendingDebtAmount = debtAmount;
        flow.Step = UserFlowStep.WaitingDebtDeadline;
        await bot.SendTextMessageAsync(chatId, "Срок возврата (ДД.ММ.ГГГГ) или 'нет':", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
        return true;
    }

    // Шаг 3: Ввод дедлайна (или пропуск)
    private async Task<bool> HandleDebtDeadlineAsync(ITelegramBotClient bot, long chatId, long userId, string text, UserFlowState flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        DateTimeOffset? deadline = null;
        if (DateTime.TryParseExact(text, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            deadline = new DateTimeOffset(d, TimeSpan.Zero);

        await debtService.CreateAsync(userId, flow.PendingDebtName!, flow.PendingDebtAmount, flow.PendingDebtType, null, deadline, ct);
        flowDict.Remove(userId);
        await bot.SendTextMessageAsync(chatId, "✅ Долг записан!", replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
        return true;
    }

    // Оплата долга
    private async Task<bool> HandleDebtPaymentAsync(ITelegramBotClient bot, long chatId, long userId, string text, UserFlowState flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        if (!FlowHelper.TryParseAmount(text, out var amount) || amount <= 0)
        {
            await bot.SendTextMessageAsync(chatId, "❌ Неверная сумма.", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
            return true;
        }

        await debtService.MakePaymentAsync(userId, flow.PendingDebtId!.Value, amount, ct);

        var debt = await debtService.GetByIdAsync(userId, flow.PendingDebtId!.Value, ct);
        if (debt != null)
        {
            var cats = await categoryService.GetUserCategoriesAsync(userId, ct);
            var type = debt.Type == DebtType.IOwe ? TransactionType.Expense : TransactionType.Income;
            var cat = cats.FirstOrDefault(x => x.Name == "Долги") ?? cats.FirstOrDefault(x => x.Type == type);
            if (cat != null)
                await transactionHandler.AddTransactionWithDescriptionAsync(bot, chatId, userId, amount, cat.Id, type, $"Возврат: {debt.PersonName}", false, ct);
        }

        flowDict.Remove(userId);
        await bot.SendTextMessageAsync(chatId, "✅ Платёж учтён!", replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
        return true;
    }
}
