using Console.Bot.Keyboards;
using Console.Commands;
using Console.Flow;
using Domain.Enums;
using Infrastructure.Services;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Console.Handlers;

// Обработчик callback-кнопок для Долгов
public class DebtCallbackHandler(
    DebtCommand debtCmd,
    IDebtService debtService) : ICallbackHandler
{
    public async Task<bool> HandleAsync(ITelegramBotClient bot, CallbackQuery cb, string data,
        UserFlowState? flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        if (!data.StartsWith("debt:")) return false;

        var userId = cb.From.Id;
        var chatId = cb.Message!.Chat.Id;
        var msgId = cb.Message.MessageId;

        if (!flowDict.TryGetValue(userId, out var dFlow))
        {
            dFlow = new UserFlowState();
            flowDict[userId] = dFlow;
        }

        // === НАВИГАЦИЯ ===
        switch (data)
        {
            case "debt:main":
                dFlow.Step = UserFlowStep.None;
                await debtCmd.ShowDashboardAsync(bot, chatId, userId, msgId, ct);
                return true;

            case "debt:noop":
                return true;

            case "debt:create":
                await bot.EditMessageTextAsync(chatId, msgId,
                    "💸 *Новый долг*\n\nВыберите тип:",
                    Telegram.Bot.Types.Enums.ParseMode.Markdown,
                    replyMarkup: DebtKeyboards.CreateType(), cancellationToken: ct);
                return true;

            case "debt:create:theyowe":
                dFlow.Step = UserFlowStep.WaitingDebtName;
                dFlow.PendingDebtType = DebtType.TheyOwe;
                await bot.EditMessageTextAsync(chatId, msgId,
                    "📥 *Новый долг: Мне должны*\n\nКто вам должен?\nВведите имя:",
                    Telegram.Bot.Types.Enums.ParseMode.Markdown,
                    replyMarkup: DebtKeyboards.Cancel(), cancellationToken: ct);
                return true;

            case "debt:create:iowe":
                dFlow.Step = UserFlowStep.WaitingDebtName;
                dFlow.PendingDebtType = DebtType.IOwe;
                await bot.EditMessageTextAsync(chatId, msgId,
                    "📤 *Новый долг: Я должен*\n\nКому вы должны?\nВведите имя:",
                    Telegram.Bot.Types.Enums.ParseMode.Markdown,
                    replyMarkup: DebtKeyboards.Cancel(), cancellationToken: ct);
                return true;
        }

        // === СПИСКИ ===
        if (data.StartsWith("debt:list:theyowe"))
        {
            dFlow.Step = UserFlowStep.WaitingDebtSelect;
            dFlow.PendingDebtType = DebtType.TheyOwe;
            var page = 0;
            if (data.Split(':').Length > 3 && int.TryParse(data.Split(':')[3], out var p)) page = p;
            dFlow.PendingListPage = page;
            await debtCmd.ShowListAsync(bot, chatId, userId, msgId, DebtType.TheyOwe, page, ct);
            return true;
        }

        if (data.StartsWith("debt:list:iowe"))
        {
            dFlow.Step = UserFlowStep.WaitingDebtSelect;
            dFlow.PendingDebtType = DebtType.IOwe;
            var page = 0;
            if (data.Split(':').Length > 3 && int.TryParse(data.Split(':')[3], out var p)) page = p;
            dFlow.PendingListPage = page;
            await debtCmd.ShowListAsync(bot, chatId, userId, msgId, DebtType.IOwe, page, ct);
            return true;
        }

        // === ДЕТАЛИ ===
        if (data.StartsWith("debt:detail:"))
        {
            if (int.TryParse(data.Split(':')[2], out var debtId))
            {
                dFlow.Step = UserFlowStep.None;
                await debtCmd.ShowDetailAsync(bot, chatId, userId, debtId, msgId, ct);
            }
            return true;
        }

        // === ИСТОРИЯ ===
        if (data.StartsWith("debt:history:"))
        {
            if (int.TryParse(data.Split(':')[2], out var debtId))
                await debtCmd.ShowHistoryAsync(bot, chatId, userId, debtId, msgId, ct);
            return true;
        }

        // === ПЛАТЁЖ ===
        if (data.StartsWith("debt:pay:"))
        {
            if (int.TryParse(data.Split(':')[2], out var debtId))
            {
                var debt = await debtService.GetByIdAsync(userId, debtId, ct);
                if (debt == null) return true;

                dFlow.Step = UserFlowStep.WaitingDebtPayment;
                dFlow.PendingDebtId = debtId;

                var isTheyOwe = debt.Type == DebtType.TheyOwe;
                var label = isTheyOwe ? "Получить" : "Внести";

                await bot.EditMessageTextAsync(chatId, msgId,
                    $"💵 *{label} платёж: {debt.PersonName}*\n\n💰 Осталось: *{debt.RemainingAmount:N0}* TJS\n\nВведите сумму:",
                    Telegram.Bot.Types.Enums.ParseMode.Markdown,
                    replyMarkup: DebtKeyboards.Cancel(), cancellationToken: ct);
            }
            return true;
        }

        // === УДАЛЕНИЕ ===
        if (data.StartsWith("debt:delete:") && !data.Contains("confirm"))
        {
            if (int.TryParse(data.Split(':')[2], out var debtId))
            {
                var debt = await debtService.GetByIdAsync(userId, debtId, ct);
                if (debt == null) return true;

                await bot.EditMessageTextAsync(chatId, msgId,
                    $"🗑 *Удаление: {debt.PersonName}*\n\n💰 Остаток: *{debt.RemainingAmount:N0}* TJS\n\n⚠️ Это действие нельзя отменить!\n\nПодтвердить?",
                    Telegram.Bot.Types.Enums.ParseMode.Markdown,
                    replyMarkup: DebtKeyboards.DeleteConfirm(debtId), cancellationToken: ct);
            }
            return true;
        }

        if (data.StartsWith("debt:delete:confirm:"))
        {
            if (int.TryParse(data.Split(':')[3], out var debtId))
                await debtCmd.DeleteAsync(bot, chatId, userId, debtId, msgId, ct);
            return true;
        }

        // === РЕДАКТИРОВАНИЕ ===
        if (data.StartsWith("debt:edit:") && data.Split(':').Length == 3)
        {
            if (int.TryParse(data.Split(':')[2], out var debtId))
            {
                await bot.EditMessageTextAsync(chatId, msgId,
                    "✏️ *Редактирование*\n\nЧто изменить?",
                    Telegram.Bot.Types.Enums.ParseMode.Markdown,
                    replyMarkup: DebtKeyboards.Edit(debtId), cancellationToken: ct);
            }
            return true;
        }

        if (data.StartsWith("debt:edit:name:"))
        {
            if (int.TryParse(data.Split(':')[3], out var debtId))
            {
                dFlow.Step = UserFlowStep.WaitingDebtEditName;
                dFlow.PendingDebtId = debtId;
                var debt = await debtService.GetByIdAsync(userId, debtId, ct);
                await bot.EditMessageTextAsync(chatId, msgId,
                    $"👤 *Новое имя*\n\nТекущее: {debt?.PersonName}\n\nВведите новое:",
                    Telegram.Bot.Types.Enums.ParseMode.Markdown,
                    replyMarkup: DebtKeyboards.Cancel(), cancellationToken: ct);
            }
            return true;
        }

        if (data.StartsWith("debt:edit:deadline:"))
        {
            if (int.TryParse(data.Split(':')[3], out var debtId))
            {
                dFlow.Step = UserFlowStep.WaitingDebtEditDeadline;
                dFlow.PendingDebtId = debtId;
                var debt = await debtService.GetByIdAsync(userId, debtId, ct);
                var current = debt?.DueDate.HasValue == true ? debt.DueDate.Value.ToString("dd.MM.yyyy") : "не установлен";
                await bot.EditMessageTextAsync(chatId, msgId,
                    $"📅 *Новый дедлайн*\n\nТекущий: {current}\n\nВведите (ДД.ММ.ГГГГ) или «нет»:",
                    Telegram.Bot.Types.Enums.ParseMode.Markdown,
                    replyMarkup: DebtKeyboards.Cancel(), cancellationToken: ct);
            }
            return true;
        }

        if (data.StartsWith("debt:edit:desc:"))
        {
            if (int.TryParse(data.Split(':')[3], out var debtId))
            {
                dFlow.Step = UserFlowStep.WaitingDebtEditDesc;
                dFlow.PendingDebtId = debtId;
                var debt = await debtService.GetByIdAsync(userId, debtId, ct);
                var current = string.IsNullOrEmpty(debt?.Description) ? "не указано" : debt.Description;
                await bot.EditMessageTextAsync(chatId, msgId,
                    $"📝 *Новое описание*\n\nТекущее: {current}\n\nВведите новое или «нет»:",
                    Telegram.Bot.Types.Enums.ParseMode.Markdown,
                    replyMarkup: DebtKeyboards.Cancel(), cancellationToken: ct);
            }
            return true;
        }

        // === ПРОПУСК ===
        if (data == "debt:skip:deadline")
        {
            dFlow.PendingDebtDeadline = null;
            dFlow.Step = UserFlowStep.WaitingDebtDescription;
            await bot.EditMessageTextAsync(chatId, msgId,
                "📅 Дедлайн: _пропущен_\n\nДобавьте описание (за что долг):",
                Telegram.Bot.Types.Enums.ParseMode.Markdown,
                replyMarkup: DebtKeyboards.Skip("debt:skip:desc"), cancellationToken: ct);
            return true;
        }

        if (data == "debt:skip:desc")
        {
            dFlow.PendingDebtDescription = null;
            // Финализация
            return await FinalizeDebtCreationAsync(bot, chatId, userId, dFlow, flowDict, ct);
        }

        // === ДОБАВИТЬ К БАЛАНСУ ===
        if (data == "debt:addbalance:yes")
        {
            dFlow.PendingAddToBalance = true;
            return await FinalizeDebtCreationAsync(bot, chatId, userId, dFlow, flowDict, ct);
        }

        if (data == "debt:addbalance:no")
        {
            dFlow.PendingAddToBalance = false;
            return await FinalizeDebtCreationAsync(bot, chatId, userId, dFlow, flowDict, ct);
        }

        return false;
    }

    private async Task<bool> FinalizeDebtCreationAsync(ITelegramBotClient bot, long chatId, long userId,
        UserFlowState flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        var debt = await debtService.CreateAsync(userId, flow.PendingDebtName!,
            flow.PendingDebtAmount, flow.PendingDebtType!.Value,
            flow.PendingDebtDescription, flow.PendingDebtDeadline, ct);

        flowDict.Remove(userId);
        await debtCmd.ShowAfterCreateAsync(bot, chatId, debt, flow.PendingAddToBalance ?? false, ct);
        return true;
    }
}
