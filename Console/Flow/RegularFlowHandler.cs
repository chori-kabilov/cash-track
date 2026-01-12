using Console.Bot.Keyboards;
using Console.Commands;
using Infrastructure.Services;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace Console.Flow;

// Обработчик текстового ввода для Регулярных платежей
public class RegularFlowHandler(
    IRegularPaymentService regularService,
    RegularPaymentCommand regularCmd) : IFlowStepHandler
{
    private static readonly UserFlowStep[] HandledSteps =
    {
        UserFlowStep.WaitingRegularName,
        UserFlowStep.WaitingRegularAmount,
        UserFlowStep.WaitingRegularDate,
        UserFlowStep.WaitingRegularSelect,
        UserFlowStep.WaitingRegularEditName,
        UserFlowStep.WaitingRegularEditAmount,
        UserFlowStep.WaitingRegularEditDay
    };

    public bool CanHandle(UserFlowStep step) => HandledSteps.Contains(step);

    public async Task<bool> HandleAsync(ITelegramBotClient bot, long chatId, long userId, string text,
        UserFlowState flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        return flow.Step switch
        {
            UserFlowStep.WaitingRegularName => await HandleNameAsync(bot, chatId, text, flow, ct),
            UserFlowStep.WaitingRegularAmount => await HandleAmountAsync(bot, chatId, text, flow, ct),
            UserFlowStep.WaitingRegularDate => await HandleDateAsync(bot, chatId, userId, text, flow, ct),
            UserFlowStep.WaitingRegularSelect => await HandleSelectAsync(bot, chatId, userId, text, flow, flowDict, ct),
            UserFlowStep.WaitingRegularEditName => await HandleEditNameAsync(bot, chatId, userId, text, flow, flowDict, ct),
            UserFlowStep.WaitingRegularEditAmount => await HandleEditAmountAsync(bot, chatId, userId, text, flow, flowDict, ct),
            UserFlowStep.WaitingRegularEditDay => await HandleEditDayAsync(bot, chatId, userId, text, flow, flowDict, ct),
            _ => false
        };
    }

    // Название
    private async Task<bool> HandleNameAsync(ITelegramBotClient bot, long chatId, string text, UserFlowState flow, CancellationToken ct)
    {
        flow.PendingRegularName = text.Trim();
        flow.Step = UserFlowStep.WaitingRegularAmount;

        await bot.SendTextMessageAsync(chatId,
            $"📋 *{flow.PendingRegularName}*\n\nВведите сумму (TJS):",
            ParseMode.Markdown, replyMarkup: RegularKeyboards.Cancel(), cancellationToken: ct);
        return true;
    }

    // Сумма
    private async Task<bool> HandleAmountAsync(ITelegramBotClient bot, long chatId, string text, UserFlowState flow, CancellationToken ct)
    {
        if (!FlowHelper.TryParseAmount(text, out var amount) || amount <= 0)
        {
            await bot.SendTextMessageAsync(chatId, "❌ Введите число:",
                replyMarkup: RegularKeyboards.Cancel(), cancellationToken: ct);
            return true;
        }

        flow.PendingRegularAmount = amount;
        flow.Step = UserFlowStep.WaitingRegularFrequency;

        await bot.SendTextMessageAsync(chatId,
            $"💰 Сумма: *{amount:N0}* TJS\n\nКак часто платить?",
            ParseMode.Markdown, replyMarkup: RegularKeyboards.Frequency(), cancellationToken: ct);
        return true;
    }

    // Обработчик Типа (но он через Callback, поэтому здесь логика только если текст введен, что маловероятно для кнопок)
    // Но нам нужно добавить в HandledSteps? Нет, CallbackHandler обрабатывает кнопки.
    // Нам нужно добавить состояние в UserFlowStep?
    // Мы добавили PendingRegularType в стейт, но enum шага?
    // UserFlowStep.WaitingRegularType?
    // Давайте проверим UserFlowStep.
    
    // Пока просто добавим логику смены шага в Handlers.
    // FlowHandler обрабатывает TEXT input.
    // Type выбирается кнопками -> RegularCallbackHandler.
    
    // День
    private async Task<bool> HandleDateAsync(ITelegramBotClient bot, long chatId, long userId, string text, UserFlowState flow, CancellationToken ct)
    {
        if (!int.TryParse(text.Trim(), out var day) || day < 1 || day > 31)
        {
            await bot.SendTextMessageAsync(chatId, "❌ Введите число от 1 до 31:",
                replyMarkup: RegularKeyboards.DayOfMonth(), cancellationToken: ct);
            return true;
        }

        flow.PendingRegularDayOfMonth = day;
        flow.Step = UserFlowStep.None;

        // Финализация (Категория по умолчанию)
        // Имитируем finalize в CallbackHandler, но здесь у нас нет доступа к нему напрямую
        // Придётся вызвать логику создания здесь или перенаправить
        // Проще вызвать создание напрямую
        
        var dayOfMonth = flow.PendingRegularDayOfMonth == 0 
            ? DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month) 
            : flow.PendingRegularDayOfMonth;

        // Категорию не ставим (будет системная при оплате) или null
        var payment = await regularService.CreateAsync(userId, flow.PendingRegularName!,
            flow.PendingRegularAmount, flow.PendingRegularFrequency, flow.PendingRegularType, null, dayOfMonth, 3, null, ct);
        
        // flowDict нужно очистить в calling code, но мы тут reference
        // Так как мы IFlowStepHandler, нам передали flowDict
        // flowDict.Remove(userId); <-- This logic is usually outside or passed in
        // Исправим сигнатуру HandleAsync чтобы принимать словарь если надо, но он уже есть
        
        // В текущем коде: HandleSelectAsync принимает flowDict. HandleDateAsync тоже должен.
        // Но в интерфейсе HandleAsync принимает flowDict.
        // Реализуем private метод Finalize или скопируем логику
        
        // ВАЖНО: Мы не можем тут вызвать regularCmd.ShowAfterCreateAsync если не прокинем
        // Но regularCmd у нас есть в конструкторе.
        
        await regularCmd.ShowAfterCreateAsync(bot, chatId, payment, ct);
        return true; 
    }

    // Выбор по номеру
    private async Task<bool> HandleSelectAsync(ITelegramBotClient bot, long chatId, long userId, string text, UserFlowState flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        if (!int.TryParse(text.Trim(), out var num) || num < 1)
        {
            await bot.SendTextMessageAsync(chatId, "❌ Введите номер:",
                replyMarkup: RegularKeyboards.Cancel(), cancellationToken: ct);
            return true;
        }

        var payments = await regularService.GetUserPaymentsAsync(userId, ct);
        if (num > payments.Count)
        {
            await bot.SendTextMessageAsync(chatId, $"❌ Нет платежа с номером {num}",
                replyMarkup: RegularKeyboards.Cancel(), cancellationToken: ct);
            return true;
        }

        var payment = payments[num - 1];
        flowDict.Remove(userId);

        var msg = await bot.SendTextMessageAsync(chatId, "📋 Загрузка...", cancellationToken: ct);
        await regularCmd.ShowDetailAsync(bot, chatId, userId, payment.Id, msg.MessageId, ct);
        return true;
    }

    // Редактирование названия
    private async Task<bool> HandleEditNameAsync(ITelegramBotClient bot, long chatId, long userId, string text, UserFlowState flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        if (flow.PendingRegularId == null) return false;

        var payment = await regularService.GetByIdAsync(userId, flow.PendingRegularId.Value, ct);
        if (payment == null) return false;

        await regularService.UpdateAsync(userId, payment.Id, text.Trim(), payment.Amount, payment.CategoryId, ct);
        flowDict.Remove(userId);

        await bot.SendTextMessageAsync(chatId,
            $"✅ Название изменено на *{text.Trim()}*",
            ParseMode.Markdown, replyMarkup: RegularKeyboards.AfterCreate(), cancellationToken: ct);
        return true;
    }

    // Редактирование суммы
    private async Task<bool> HandleEditAmountAsync(ITelegramBotClient bot, long chatId, long userId, string text, UserFlowState flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        if (flow.PendingRegularId == null) return false;

        if (!FlowHelper.TryParseAmount(text, out var amount) || amount <= 0)
        {
            await bot.SendTextMessageAsync(chatId, "❌ Введите число:",
                replyMarkup: RegularKeyboards.Cancel(), cancellationToken: ct);
            return true;
        }

        var payment = await regularService.GetByIdAsync(userId, flow.PendingRegularId.Value, ct);
        if (payment == null) return false;

        await regularService.UpdateAsync(userId, payment.Id, payment.Name, amount, payment.CategoryId, ct);
        flowDict.Remove(userId);

        await bot.SendTextMessageAsync(chatId,
            $"✅ Сумма изменена на *{amount:N0}* TJS",
            ParseMode.Markdown, replyMarkup: RegularKeyboards.AfterCreate(), cancellationToken: ct);
        return true;
    }

    // Редактирование дня
    private async Task<bool> HandleEditDayAsync(ITelegramBotClient bot, long chatId, long userId, string text, UserFlowState flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        if (flow.PendingRegularId == null) return false;

        if (!int.TryParse(text.Trim(), out var day) || day < 1 || day > 31)
        {
            await bot.SendTextMessageAsync(chatId, "❌ Введите число от 1 до 31:",
                replyMarkup: RegularKeyboards.Cancel(), cancellationToken: ct);
            return true;
        }

        await regularService.UpdateDayAsync(userId, flow.PendingRegularId.Value, day, ct);
        flowDict.Remove(userId);

        await bot.SendTextMessageAsync(chatId,
            $"✅ Дата изменена на *{day} числа*",
            ParseMode.Markdown, replyMarkup: RegularKeyboards.AfterCreate(), cancellationToken: ct);
        return true;
    }
}
