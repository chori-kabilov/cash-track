using System.Globalization;
using Console.Bot.Keyboards;
using Console.Commands;
using Domain.Enums;
using Infrastructure.Services;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace Console.Flow;

// Обработчик текстового ввода для Долгов
public class DebtFlowHandler(
    IDebtService debtService,
    DebtCommand debtCmd) : IFlowStepHandler
{
    private static readonly UserFlowStep[] HandledSteps =
    {
        UserFlowStep.WaitingDebtName,
        UserFlowStep.WaitingDebtAmount,
        UserFlowStep.WaitingDebtDeadline,
        UserFlowStep.WaitingDebtDescription,
        UserFlowStep.WaitingDebtPayment,
        UserFlowStep.WaitingDebtSelect,
        UserFlowStep.WaitingDebtEditName,
        UserFlowStep.WaitingDebtEditDeadline,
        UserFlowStep.WaitingDebtEditDesc
    };

    public bool CanHandle(UserFlowStep step) => HandledSteps.Contains(step);

    public async Task<bool> HandleAsync(ITelegramBotClient bot, long chatId, long userId, string text,
        UserFlowState flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        return flow.Step switch
        {
            UserFlowStep.WaitingDebtName => await HandleNameAsync(bot, chatId, text, flow, ct),
            UserFlowStep.WaitingDebtAmount => await HandleAmountAsync(bot, chatId, text, flow, ct),
            UserFlowStep.WaitingDebtDeadline => await HandleDeadlineAsync(bot, chatId, text, flow, ct),
            UserFlowStep.WaitingDebtDescription => await HandleDescriptionAsync(bot, chatId, userId, text, flow, flowDict, ct),
            UserFlowStep.WaitingDebtPayment => await HandlePaymentAsync(bot, chatId, userId, text, flow, flowDict, ct),
            UserFlowStep.WaitingDebtSelect => await HandleSelectAsync(bot, chatId, userId, text, flow, flowDict, ct),
            UserFlowStep.WaitingDebtEditName => await HandleEditNameAsync(bot, chatId, userId, text, flow, flowDict, ct),
            UserFlowStep.WaitingDebtEditDeadline => await HandleEditDeadlineAsync(bot, chatId, userId, text, flow, flowDict, ct),
            UserFlowStep.WaitingDebtEditDesc => await HandleEditDescAsync(bot, chatId, userId, text, flow, flowDict, ct),
            _ => false
        };
    }

    // Имя человека
    private async Task<bool> HandleNameAsync(ITelegramBotClient bot, long chatId, string text, UserFlowState flow, CancellationToken ct)
    {
        flow.PendingDebtName = text.Trim();
        flow.Step = UserFlowStep.WaitingDebtAmount;

        var typeLabel = flow.PendingDebtType == DebtType.TheyOwe ? "должен вам" : "вы должны";
        await bot.SendTextMessageAsync(chatId,
            $"👤 {flow.PendingDebtName} {typeLabel}\n\nВведите сумму (в TJS):",
            ParseMode.Markdown, replyMarkup: DebtKeyboards.Cancel(), cancellationToken: ct);
        return true;
    }

    // Сумма
    private async Task<bool> HandleAmountAsync(ITelegramBotClient bot, long chatId, string text, UserFlowState flow, CancellationToken ct)
    {
        if (!FlowHelper.TryParseAmount(text, out var amount) || amount <= 0)
        {
            await bot.SendTextMessageAsync(chatId, "❌ Введите число:",
                replyMarkup: DebtKeyboards.Cancel(), cancellationToken: ct);
            return true;
        }

        flow.PendingDebtAmount = amount;
        flow.Step = UserFlowStep.WaitingDebtDeadline;

        await bot.SendTextMessageAsync(chatId,
            $"💰 Сумма: *{amount:N0}* TJS\n\nУкажите дедлайн (ДД.ММ.ГГГГ):",
            ParseMode.Markdown, replyMarkup: DebtKeyboards.Skip("debt:skip:deadline"), cancellationToken: ct);
        return true;
    }

    // Дедлайн
    private async Task<bool> HandleDeadlineAsync(ITelegramBotClient bot, long chatId, string text, UserFlowState flow, CancellationToken ct)
    {
        if (DateTime.TryParseExact(text.Trim(), "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
        {
            flow.PendingDebtDeadline = new DateTimeOffset(d, TimeSpan.Zero);
        }
        else
        {
            await bot.SendTextMessageAsync(chatId, "❌ Формат: ДД.ММ.ГГГГ",
                replyMarkup: DebtKeyboards.Skip("debt:skip:deadline"), cancellationToken: ct);
            return true;
        }

        flow.Step = UserFlowStep.WaitingDebtDescription;
        await bot.SendTextMessageAsync(chatId,
            $"📅 Дедлайн: *{flow.PendingDebtDeadline:dd.MM.yyyy}*\n\nДобавьте описание (за что долг):",
            ParseMode.Markdown, replyMarkup: DebtKeyboards.Skip("debt:skip:desc"), cancellationToken: ct);
        return true;
    }

    // Описание
    private async Task<bool> HandleDescriptionAsync(ITelegramBotClient bot, long chatId, long userId, string text, UserFlowState flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        flow.PendingDebtDescription = text.Trim();

        // Если "Я должен" → спросить про баланс
        if (flow.PendingDebtType == DebtType.IOwe)
        {
            flow.Step = UserFlowStep.None;
            await bot.SendTextMessageAsync(chatId,
                $"📝 Описание: {flow.PendingDebtDescription}\n\nДобавить эту сумму к балансу?\n(Если деньги уже получены)",
                ParseMode.Markdown, replyMarkup: DebtKeyboards.AddToBalance(), cancellationToken: ct);
            return true;
        }

        // Иначе — создать сразу
        var debt = await debtService.CreateAsync(userId, flow.PendingDebtName!,
            flow.PendingDebtAmount, flow.PendingDebtType!.Value,
            flow.PendingDebtDescription, flow.PendingDebtDeadline, ct);
        flowDict.Remove(userId);
        await debtCmd.ShowAfterCreateAsync(bot, chatId, debt, false, ct);
        return true;
    }

    // Платёж
    private async Task<bool> HandlePaymentAsync(ITelegramBotClient bot, long chatId, long userId, string text, UserFlowState flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        if (!FlowHelper.TryParseAmount(text, out var amount) || amount <= 0)
        {
            await bot.SendTextMessageAsync(chatId, "❌ Введите сумму:",
                replyMarkup: DebtKeyboards.Cancel(), cancellationToken: ct);
            return true;
        }

        var debtId = flow.PendingDebtId ?? 0;
        if (debtId == 0) return false;

        var debt = await debtService.GetByIdAsync(userId, debtId, ct);
        if (debt != null && amount > debt.RemainingAmount)
            amount = debt.RemainingAmount;

        flowDict.Remove(userId);
        await debtCmd.RecordPaymentAsync(bot, chatId, userId, debtId, amount, ct);
        return true;
    }

    // Выбор по номеру
    private async Task<bool> HandleSelectAsync(ITelegramBotClient bot, long chatId, long userId, string text, UserFlowState flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        if (!int.TryParse(text.Trim(), out var num) || num < 1)
        {
            await bot.SendTextMessageAsync(chatId, "❌ Введите номер:",
                replyMarkup: DebtKeyboards.Cancel(), cancellationToken: ct);
            return true;
        }

        var debts = await debtService.GetByTypeAsync(userId, flow.PendingDebtType ?? DebtType.TheyOwe, ct);
        if (num > debts.Count)
        {
            await bot.SendTextMessageAsync(chatId, $"❌ Нет долга с номером {num}",
                replyMarkup: DebtKeyboards.Cancel(), cancellationToken: ct);
            return true;
        }

        var debt = debts[num - 1];
        flowDict.Remove(userId);

        // Отправить детали
        var msg = await bot.SendTextMessageAsync(chatId, "📋 Загрузка...", cancellationToken: ct);
        await debtCmd.ShowDetailAsync(bot, chatId, userId, debt.Id, msg.MessageId, ct);
        return true;
    }

    // Редактирование имени
    private async Task<bool> HandleEditNameAsync(ITelegramBotClient bot, long chatId, long userId, string text, UserFlowState flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        if (flow.PendingDebtId == null) return false;

        var debt = await debtService.GetByIdAsync(userId, flow.PendingDebtId.Value, ct);
        if (debt == null) return false;

        await debtService.UpdateAsync(userId, debt.Id, text.Trim(), debt.Description, debt.DueDate, ct);
        flowDict.Remove(userId);

        await bot.SendTextMessageAsync(chatId,
            $"✅ Имя изменено на *{text.Trim()}*",
            ParseMode.Markdown, replyMarkup: DebtKeyboards.AfterCreate(), cancellationToken: ct);
        return true;
    }

    // Редактирование дедлайна
    private async Task<bool> HandleEditDeadlineAsync(ITelegramBotClient bot, long chatId, long userId, string text, UserFlowState flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        if (flow.PendingDebtId == null) return false;

        var debt = await debtService.GetByIdAsync(userId, flow.PendingDebtId.Value, ct);
        if (debt == null) return false;

        DateTimeOffset? deadline = null;
        if (!text.Contains("нет", StringComparison.OrdinalIgnoreCase))
        {
            if (DateTime.TryParseExact(text.Trim(), "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                deadline = new DateTimeOffset(d, TimeSpan.Zero);
            else
            {
                await bot.SendTextMessageAsync(chatId, "❌ Формат: ДД.ММ.ГГГГ (или «нет»):",
                    replyMarkup: DebtKeyboards.Cancel(), cancellationToken: ct);
                return true;
            }
        }

        await debtService.UpdateAsync(userId, debt.Id, debt.PersonName, debt.Description, deadline, ct);
        flowDict.Remove(userId);

        var msg = deadline.HasValue ? $"✅ Дедлайн: *{deadline:dd.MM.yyyy}*" : "✅ Дедлайн убран";
        await bot.SendTextMessageAsync(chatId, msg, ParseMode.Markdown,
            replyMarkup: DebtKeyboards.AfterCreate(), cancellationToken: ct);
        return true;
    }

    // Редактирование описания
    private async Task<bool> HandleEditDescAsync(ITelegramBotClient bot, long chatId, long userId, string text, UserFlowState flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        if (flow.PendingDebtId == null) return false;

        var debt = await debtService.GetByIdAsync(userId, flow.PendingDebtId.Value, ct);
        if (debt == null) return false;

        string? desc = text.Contains("нет", StringComparison.OrdinalIgnoreCase) ? null : text.Trim();
        await debtService.UpdateAsync(userId, debt.Id, debt.PersonName, desc, debt.DueDate, ct);
        flowDict.Remove(userId);

        await bot.SendTextMessageAsync(chatId,
            desc != null ? $"✅ Описание: *{desc}*" : "✅ Описание убрано",
            ParseMode.Markdown, replyMarkup: DebtKeyboards.AfterCreate(), cancellationToken: ct);
        return true;
    }
}
