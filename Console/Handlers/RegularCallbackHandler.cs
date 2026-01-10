using Console.Bot.Keyboards;
using Console.Commands;
using Console.Flow;
using Domain.Enums;
using Infrastructure.Services;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Console.Handlers;

// Обработчик callback-кнопок для Регулярных платежей
public class RegularCallbackHandler(
    RegularPaymentCommand regularCmd,
    IRegularPaymentService regularService,
    ICategoryService categoryService) : ICallbackHandler
{
    public async Task<bool> HandleAsync(ITelegramBotClient bot, CallbackQuery cb, string data,
        UserFlowState? flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        if (!data.StartsWith("regular:")) return false;

        var userId = cb.From.Id;
        var chatId = cb.Message!.Chat.Id;
        var msgId = cb.Message.MessageId;

        if (!flowDict.TryGetValue(userId, out var rFlow))
        {
            rFlow = new UserFlowState();
            flowDict[userId] = rFlow;
        }

        // === НАВИГАЦИЯ ===
        switch (data)
        {
            case "regular:main":
                rFlow.Step = UserFlowStep.None;
                await regularCmd.ShowDashboardAsync(bot, chatId, userId, msgId, ct);
                return true;

            case "regular:noop":
                return true;

            case "regular:list":
                rFlow.Step = UserFlowStep.WaitingRegularSelect;
                rFlow.PendingListPage = 0;
                await regularCmd.ShowListAsync(bot, chatId, userId, msgId, 0, ct);
                return true;

            case "regular:create":
                rFlow.Step = UserFlowStep.WaitingRegularName;
                await bot.EditMessageTextAsync(chatId, msgId,
                    "📝 *Новый регулярный платёж*\n\nВведите название:",
                    Telegram.Bot.Types.Enums.ParseMode.Markdown,
                    replyMarkup: RegularKeyboards.Cancel(), cancellationToken: ct);
                return true;
        }

        // === ПАГИНАЦИЯ ===
        if (data.StartsWith("regular:list:"))
        {
            if (int.TryParse(data.Split(':')[2], out var page))
            {
                rFlow.Step = UserFlowStep.WaitingRegularSelect;
                rFlow.PendingListPage = page;
                await regularCmd.ShowListAsync(bot, chatId, userId, msgId, page, ct);
            }
            return true;
        }

        // === ДЕТАЛИ ===
        if (data.StartsWith("regular:detail:"))
        {
            if (int.TryParse(data.Split(':')[2], out var paymentId))
            {
                rFlow.Step = UserFlowStep.None;
                await regularCmd.ShowDetailAsync(bot, chatId, userId, paymentId, msgId, ct);
            }
            return true;
        }

        // === ИСТОРИЯ ===
        if (data.StartsWith("regular:history:"))
        {
            if (int.TryParse(data.Split(':')[2], out var paymentId))
                await regularCmd.ShowHistoryAsync(bot, chatId, userId, paymentId, msgId, ct);
            return true;
        }

        // === ОПЛАТИТЬ ===
        if (data.StartsWith("regular:pay:"))
        {
            if (int.TryParse(data.Split(':')[2], out var paymentId))
                await regularCmd.MarkAsPaidAsync(bot, chatId, userId, paymentId, ct);
            return true;
        }

        // === ПАУЗА/ВОЗОБНОВЛЕНИЕ ===
        if (data.StartsWith("regular:pause:"))
        {
            if (int.TryParse(data.Split(':')[2], out var paymentId))
            {
                await regularService.SetPausedAsync(userId, paymentId, true, ct);
                await regularCmd.ShowDetailAsync(bot, chatId, userId, paymentId, msgId, ct);
            }
            return true;
        }

        if (data.StartsWith("regular:resume:"))
        {
            if (int.TryParse(data.Split(':')[2], out var paymentId))
            {
                await regularService.SetPausedAsync(userId, paymentId, false, ct);
                await regularCmd.ShowDetailAsync(bot, chatId, userId, paymentId, msgId, ct);
            }
            return true;
        }

        // === УДАЛЕНИЕ ===
        if (data.StartsWith("regular:delete:") && !data.Contains("confirm"))
        {
            if (int.TryParse(data.Split(':')[2], out var paymentId))
            {
                var payment = await regularService.GetByIdAsync(userId, paymentId, ct);
                if (payment == null) return true;
                await bot.EditMessageTextAsync(chatId, msgId,
                    $"🗑 *Удаление: {payment.Name}*\n\n💰 {payment.Amount:N0} TJS\n\n⚠️ История платежей будет потеряна!\n\nПодтвердить?",
                    Telegram.Bot.Types.Enums.ParseMode.Markdown,
                    replyMarkup: RegularKeyboards.DeleteConfirm(paymentId), cancellationToken: ct);
            }
            return true;
        }

        if (data.StartsWith("regular:delete:confirm:"))
        {
            if (int.TryParse(data.Split(':')[3], out var paymentId))
                await regularCmd.DeleteAsync(bot, chatId, userId, paymentId, msgId, ct);
            return true;
        }

        // === РЕДАКТИРОВАНИЕ ===
        if (data.StartsWith("regular:edit:") && data.Split(':').Length == 3)
        {
            if (int.TryParse(data.Split(':')[2], out var paymentId))
            {
                await bot.EditMessageTextAsync(chatId, msgId,
                    "✏️ *Редактирование*\n\nЧто изменить?",
                    Telegram.Bot.Types.Enums.ParseMode.Markdown,
                    replyMarkup: RegularKeyboards.Edit(paymentId), cancellationToken: ct);
            }
            return true;
        }

        if (data.StartsWith("regular:edit:name:"))
        {
            if (int.TryParse(data.Split(':')[3], out var paymentId))
            {
                rFlow.Step = UserFlowStep.WaitingRegularEditName;
                rFlow.PendingRegularId = paymentId;
                var payment = await regularService.GetByIdAsync(userId, paymentId, ct);
                await bot.EditMessageTextAsync(chatId, msgId,
                    $"📝 *Новое название*\n\nТекущее: {payment?.Name}\n\nВведите:",
                    Telegram.Bot.Types.Enums.ParseMode.Markdown,
                    replyMarkup: RegularKeyboards.Cancel(), cancellationToken: ct);
            }
            return true;
        }

        if (data.StartsWith("regular:edit:amount:"))
        {
            if (int.TryParse(data.Split(':')[3], out var paymentId))
            {
                rFlow.Step = UserFlowStep.WaitingRegularEditAmount;
                rFlow.PendingRegularId = paymentId;
                var payment = await regularService.GetByIdAsync(userId, paymentId, ct);
                await bot.EditMessageTextAsync(chatId, msgId,
                    $"💰 *Новая сумма*\n\nТекущая: {payment?.Amount:N0} TJS\n\nВведите:",
                    Telegram.Bot.Types.Enums.ParseMode.Markdown,
                    replyMarkup: RegularKeyboards.Cancel(), cancellationToken: ct);
            }
            return true;
        }

        if (data.StartsWith("regular:edit:day:"))
        {
            if (int.TryParse(data.Split(':')[3], out var paymentId))
            {
                rFlow.Step = UserFlowStep.WaitingRegularEditDay;
                rFlow.PendingRegularId = paymentId;
                var payment = await regularService.GetByIdAsync(userId, paymentId, ct);
                var current = payment?.DayOfMonth?.ToString() ?? "не установлен";
                await bot.EditMessageTextAsync(chatId, msgId,
                    $"📅 *Новая дата*\n\nТекущая: {current} числа\n\nВведите число (1-31):",
                    Telegram.Bot.Types.Enums.ParseMode.Markdown,
                    replyMarkup: RegularKeyboards.Cancel(), cancellationToken: ct);
            }
            return true;
        }

        if (data.StartsWith("regular:edit:cat:"))
        {
            if (int.TryParse(data.Split(':')[3], out var paymentId))
            {
                rFlow.Step = UserFlowStep.WaitingRegularEditCat;
                rFlow.PendingRegularId = paymentId;
                var cats = await categoryService.GetByTypeAsync(userId, TransactionType.Expense, ct);
                var buttons = cats.Select(c => 
                    new[] { Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData($"{c.Icon} {c.Name}", $"regular:setcat:{paymentId}:{c.Id}") }
                ).ToList();
                buttons.Add(new[] { Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("⏭ Без категории", $"regular:setcat:{paymentId}:0") });
                buttons.Add(new[] { Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("❌ Отмена", $"regular:detail:{paymentId}") });
                await bot.EditMessageTextAsync(chatId, msgId,
                    "📂 *Выберите категорию:*",
                    Telegram.Bot.Types.Enums.ParseMode.Markdown,
                    replyMarkup: new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(buttons), cancellationToken: ct);
            }
            return true;
        }

        if (data.StartsWith("regular:setcat:"))
        {
            var parts = data.Split(':');
            if (parts.Length >= 4 && int.TryParse(parts[2], out var paymentId) && int.TryParse(parts[3], out var catId))
            {
                var payment = await regularService.GetByIdAsync(userId, paymentId, ct);
                if (payment != null)
                {
                    await regularService.UpdateAsync(userId, paymentId, payment.Name, payment.Amount, catId == 0 ? null : catId, ct);
                    rFlow.Step = UserFlowStep.None;
                    await regularCmd.ShowDetailAsync(bot, chatId, userId, paymentId, msgId, ct);
                }
            }
            return true;
        }

        // === ЧАСТОТА (ПРИ СОЗДАНИИ) ===
        if (data.StartsWith("regular:freq:"))
        {
            var freqStr = data.Split(':')[2];
            PaymentFrequency freq = freqStr switch
            {
                "monthly" => PaymentFrequency.Monthly,
                "weekly" => PaymentFrequency.Weekly,
                "yearly" => PaymentFrequency.Yearly,
                _ => PaymentFrequency.Monthly
            };
            rFlow.PendingRegularFrequency = freq;
            rFlow.Step = UserFlowStep.WaitingRegularDate;

            var datePrompt = freq == PaymentFrequency.Weekly 
                ? "Введите день недели (1=Пн, 7=Вс):" 
                : "Введите число (1-31):";

            await bot.EditMessageTextAsync(chatId, msgId,
                $"🔄 Периодичность: *{GetFreqName(freq)}*\n\n{datePrompt}",
                Telegram.Bot.Types.Enums.ParseMode.Markdown,
                replyMarkup: RegularKeyboards.DayOfMonth(), cancellationToken: ct);
            return true;
        }

        if (data == "regular:day:last")
        {
            rFlow.PendingRegularDayOfMonth = 0; // 0 = последний день
            return await ShowCategorySelectionAsync(bot, chatId, userId, msgId, rFlow, ct);
        }

        if (data.StartsWith("regular:cat:"))
        {
            if (data == "regular:cat:skip")
            {
                rFlow.PendingCategoryId = null;
                return await FinalizeCreationAsync(bot, chatId, userId, rFlow, flowDict, ct);
            }
            if (int.TryParse(data.Split(':')[2], out var catId))
            {
                rFlow.PendingCategoryId = catId;
                return await FinalizeCreationAsync(bot, chatId, userId, rFlow, flowDict, ct);
            }
            return true;
        }

        return false;
    }

    private async Task<bool> ShowCategorySelectionAsync(ITelegramBotClient bot, long chatId, long userId, int msgId, UserFlowState flow, CancellationToken ct)
    {
        flow.Step = UserFlowStep.None;
        var cats = await categoryService.GetByTypeAsync(userId, TransactionType.Expense, ct);
        var buttons = cats.Select(c => 
            new[] { Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData($"{c.Icon} {c.Name}", $"regular:cat:{c.Id}") }
        ).ToList();
        buttons.Add(new[] { Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("⏭ Без категории", "regular:cat:skip") });
        buttons.Add(new[] { Telegram.Bot.Types.ReplyMarkups.InlineKeyboardButton.WithCallbackData("❌ Отмена", "regular:main") });
        await bot.EditMessageTextAsync(chatId, msgId,
            "📂 Выберите категорию:",
            Telegram.Bot.Types.Enums.ParseMode.Markdown,
            replyMarkup: new Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup(buttons), cancellationToken: ct);
        return true;
    }

    private async Task<bool> FinalizeCreationAsync(ITelegramBotClient bot, long chatId, long userId, UserFlowState flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        var dayOfMonth = flow.PendingRegularDayOfMonth == 0 
            ? DateTime.DaysInMonth(DateTime.UtcNow.Year, DateTime.UtcNow.Month) 
            : flow.PendingRegularDayOfMonth;

        var payment = await regularService.CreateAsync(userId, flow.PendingRegularName!,
            flow.PendingRegularAmount, flow.PendingRegularFrequency, flow.PendingCategoryId, dayOfMonth, 3, null, ct);

        flowDict.Remove(userId);
        await regularCmd.ShowAfterCreateAsync(bot, chatId, payment, ct);
        return true;
    }

    private static string GetFreqName(PaymentFrequency freq) => freq switch
    {
        PaymentFrequency.Daily => "Ежедневно",
        PaymentFrequency.Weekly => "Еженедельно",
        PaymentFrequency.Monthly => "Ежемесячно",
        PaymentFrequency.Yearly => "Ежегодно",
        _ => "Другое"
    };
}
