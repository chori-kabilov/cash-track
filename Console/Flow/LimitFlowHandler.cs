using Console.Bot;
using Infrastructure.Services;
using Telegram.Bot;

namespace Console.Flow;

// Обработчик шагов создания лимитов
public class LimitFlowHandler(
    ILimitService limitService,
    ICategoryService categoryService) : IFlowStepHandler
{
    private static readonly UserFlowStep[] HandledSteps = 
    {
        UserFlowStep.WaitingLimitCategory,
        UserFlowStep.WaitingLimitAmount
    };

    public bool CanHandle(UserFlowStep step) => HandledSteps.Contains(step);

    public async Task<bool> HandleAsync(ITelegramBotClient bot, long chatId, long userId, string text, 
        UserFlowState flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        return flow.Step switch
        {
            UserFlowStep.WaitingLimitAmount => await HandleLimitAmountAsync(bot, chatId, userId, text, flow, flowDict, ct),
            _ => false
        };
    }

    // Ввод суммы лимита
    private async Task<bool> HandleLimitAmountAsync(ITelegramBotClient bot, long chatId, long userId, string text, UserFlowState flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        if (!FlowHelper.TryParseAmount(text, out var amount) || amount <= 0)
        {
            await bot.SendTextMessageAsync(chatId, "❌ Неверная сумма.", replyMarkup: BotInlineKeyboards.Cancel(), cancellationToken: ct);
            return true;
        }

        if (!flow.PendingLimitCategoryId.HasValue)
        {
            await bot.SendTextMessageAsync(chatId, "❌ Категория не выбрана.", replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
            flowDict.Remove(userId);
            return true;
        }

        await limitService.CreateAsync(userId, flow.PendingLimitCategoryId.Value, amount, ct);
        
        var category = await categoryService.GetCategoryByIdAsync(userId, flow.PendingLimitCategoryId.Value, ct);
        var catName = category != null ? $"{category.Icon} {category.Name}" : "категория";
        
        flowDict.Remove(userId);
        await bot.SendTextMessageAsync(chatId, $"✅ Лимит создан!\n\n{catName}: {amount:F0} / месяц", replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
        return true;
    }
}
