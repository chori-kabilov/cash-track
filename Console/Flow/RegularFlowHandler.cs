using System.Globalization;
using Console.Bot;
using Infrastructure.Services;
using Telegram.Bot;

namespace Console.Flow;

// Обработчик шагов создания регулярных платежей
public class RegularFlowHandler(IRegularPaymentService regularPaymentService) : IFlowStepHandler
{
    private static readonly UserFlowStep[] HandledSteps = 
    {
        UserFlowStep.WaitingRegularName,
        UserFlowStep.WaitingRegularAmount,
        UserFlowStep.WaitingRegularFrequency,
        UserFlowStep.WaitingRegularDate
    };

    public bool CanHandle(UserFlowStep step) => HandledSteps.Contains(step);

    public async Task<bool> HandleAsync(ITelegramBotClient bot, long chatId, long userId, string text, 
        UserFlowState flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        return flow.Step switch
        {
            UserFlowStep.WaitingRegularName => await HandleRegularNameAsync(bot, chatId, text, flow, ct),
            UserFlowStep.WaitingRegularAmount => await HandleRegularAmountAsync(bot, chatId, text, flow, ct),
            UserFlowStep.WaitingRegularDate => await HandleRegularDateAsync(bot, chatId, userId, text, flow, flowDict, ct),
            _ => false
        };
    }

    // Шаг 1: Ввод названия платежа
    private async Task<bool> HandleRegularNameAsync(ITelegramBotClient bot, long chatId, string text, UserFlowState flow, CancellationToken ct)
    {
        flow.PendingRegularName = text;
        flow.Step = UserFlowStep.WaitingRegularAmount;
        await bot.SendTextMessageAsync(chatId, "Введите сумму:", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
        return true;
    }

    // Шаг 2: Ввод суммы
    private async Task<bool> HandleRegularAmountAsync(ITelegramBotClient bot, long chatId, string text, UserFlowState flow, CancellationToken ct)
    {
        if (!FlowHelper.TryParseAmount(text, out var regAmount) || regAmount <= 0)
        {
            await bot.SendTextMessageAsync(chatId, "❌ Неверная сумма.", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
            return true;
        }
        flow.PendingRegularAmount = regAmount;
        flow.Step = UserFlowStep.WaitingRegularFrequency;
        await bot.SendTextMessageAsync(chatId, "Как часто?", replyMarkup: FlowHelper.FrequencyKeyboard(), cancellationToken: ct);
        return true;
    }

    // Шаг 4: Ввод даты следующего платежа
    private async Task<bool> HandleRegularDateAsync(ITelegramBotClient bot, long chatId, long userId, string text, UserFlowState flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        if (!DateTime.TryParseExact(text, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
        {
            await bot.SendTextMessageAsync(chatId, "❌ Неверная дата.", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
            return true;
        }

        await regularPaymentService.CreateAsync(userId, flow.PendingRegularName!, flow.PendingRegularAmount, 
            flow.PendingRegularFrequency, null, null, 3, new DateTimeOffset(d, TimeSpan.Zero), ct);
        flowDict.Remove(userId);
        await bot.SendTextMessageAsync(chatId, "✅ Платеж создан!", replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
        return true;
    }
}
