using System.Text;
using Console.Bot.Keyboards;
using Console.Flow;
using Domain.Enums;
using Infrastructure.Services;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace Console.Commands;

// Команда управления Долгами
public class DebtCommand(
    IDebtService debtService,
    IAccountService accountService,
    ITransactionService transactionService,
    ICategoryService categoryService)
{
    private const string IncomeCategoryName = "← Возврат долга";
    private const string ExpenseCategoryName = "→ Выплата долга";

    // Точка входа
    public async Task ExecuteAsync(ITelegramBotClient bot, long chatId, long userId, CancellationToken ct, int? messageId = null, string? callbackQueryId = null)
    {
        if (messageId.HasValue)
            await ShowDashboardAsync(bot, chatId, userId, messageId.Value, ct, callbackQueryId);
        else
        {
            var msg = await bot.SendTextMessageAsync(chatId, "💸 Загрузка...", cancellationToken: ct);
            await ShowDashboardAsync(bot, chatId, userId, msg.MessageId, ct, callbackQueryId);
        }
    }

    // === ЭКРАНЫ ===

    // Дашборд
    public async Task ShowDashboardAsync(ITelegramBotClient bot, long chatId, long userId, int msgId, CancellationToken ct, string? callbackQueryId = null)
    {
        var (theyOwe, theyOweCount, iOwe, iOweCount) = await debtService.GetSummaryAsync(userId, ct);
        var overdue = await debtService.GetOverdueDebtsAsync(userId, ct);

        if (theyOweCount == 0 && iOweCount == 0)
        {
            await CommandHelpers.SafeEditMessageAsync(bot, chatId, msgId,
                "💸 *Долги*\n\nУ вас пока нет активных долгов.\nЕсли есть добавьте!",
                DebtKeyboards.Empty(), ct, callbackQueryId);
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine("💸 *Ваши долги*\n");
        sb.AppendLine("📊 *Сводка:*");
        sb.AppendLine($"🟢 Мне должны: *{theyOwe:N0}* TJS ({theyOweCount} чел.)");
        sb.AppendLine($"🔴 Я должен: *{iOwe:N0}* TJS ({iOweCount} чел.)");
        sb.AppendLine("━━━━━━━━━━━━");
        var net = theyOwe - iOwe;
        var netSign = net >= 0 ? "+" : "";
        sb.AppendLine($"💰 Чистая позиция: *{netSign}{net:N0}* TJS");

        if (overdue.Any())
        {
            sb.AppendLine($"\n⚠️ *Просрочено:* {overdue.Count}");
            foreach (var d in overdue.Take(2))
            {
                var days = (DateTimeOffset.UtcNow - d.DueDate!.Value).Days;
                sb.AppendLine($"• {d.PersonName} — {d.RemainingAmount:N0} TJS ({days} дн.)");
            }
        }

        await CommandHelpers.SafeEditMessageAsync(bot, chatId, msgId, sb.ToString(),
            DebtKeyboards.Dashboard(theyOwe > 0 || theyOweCount > 0, iOwe > 0 || iOweCount > 0), ct, callbackQueryId);
    }

    // Список долгов по типу
    public async Task ShowListAsync(ITelegramBotClient bot, long chatId, long userId, int msgId, DebtType type, int page, CancellationToken ct, string? callbackQueryId = null)
    {
        var debts = await debtService.GetByTypeAsync(userId, type, ct);
        var typeLabel = type == DebtType.TheyOwe ? "📥 *Мне должны*" : "📤 *Я должен*";
        var typeCode = type == DebtType.TheyOwe ? "theyowe" : "iowe";

        if (!debts.Any())
        {
            await CommandHelpers.SafeEditMessageAsync(bot, chatId, msgId,
                $"{typeLabel}\n\n_Пусто._",
                DebtKeyboards.List(0, 1, typeCode), ct, callbackQueryId);
            return;
        }

        int pageSize = 5;
        var totalPages = (int)Math.Ceiling((double)debts.Count / pageSize);
        if (page < 0) page = 0;
        if (page >= totalPages) page = totalPages - 1;

        var sb = new StringBuilder();
        sb.AppendLine(typeLabel);
        sb.AppendLine($"*Страница {page + 1} из {totalPages}*\n");

        var pageDebts = debts.Skip(page * pageSize).Take(pageSize).ToList();
        var startNum = page * pageSize + 1;

        foreach (var (d, idx) in pageDebts.Select((d, i) => (d, i)))
        {
            var num = startNum + idx;
            var icon = d.DueDate.HasValue && d.DueDate < DateTimeOffset.UtcNow ? "⚠️" : "👤";
            var deadlineText = d.DueDate.HasValue
                ? $" (до {d.DueDate:dd.MM})"
                : "";
            sb.AppendLine($"{num}. {icon} *{d.PersonName}* — {d.RemainingAmount:N0} TJS{deadlineText}");
        }

        sb.AppendLine("\n👇 *Введите номер для деталей:*");

        await CommandHelpers.SafeEditMessageAsync(bot, chatId, msgId, sb.ToString(),
            DebtKeyboards.List(page, totalPages, typeCode), ct, callbackQueryId);
    }

    // Детали долга
    public async Task ShowDetailAsync(ITelegramBotClient bot, long chatId, long userId, int debtId, int msgId, CancellationToken ct, string? callbackQueryId = null)
    {
        var debt = await debtService.GetByIdAsync(userId, debtId, ct);
        if (debt == null) { await ShowDashboardAsync(bot, chatId, userId, msgId, ct, callbackQueryId); return; }

        var isTheyOwe = debt.Type == DebtType.TheyOwe;
        var typeIcon = isTheyOwe ? "📥" : "📤";
        var typeLabel = isTheyOwe ? "вам должен" : "вы должны";
        var percent = debt.Amount > 0 ? ((debt.Amount - debt.RemainingAmount) / debt.Amount) * 100 : 0;

        var sb = new StringBuilder();
        sb.AppendLine($"{typeIcon} *{debt.PersonName}* {typeLabel}\n");
        sb.AppendLine($"💰 Осталось: *{debt.RemainingAmount:N0}* TJS");
        sb.AppendLine($"🎯 Изначально: {debt.Amount:N0} TJS");
        sb.AppendLine($"📊 {BuildProgressBar(percent)} *{percent:N0}%* погашено");

        if (debt.DueDate.HasValue)
        {
            var days = (debt.DueDate.Value - DateTimeOffset.UtcNow).Days;
            var status = days < 0 ? $"просрочен на {-days} дн." : $"{days} дн.";
            sb.AppendLine($"\n📅 Дедлайн: {debt.DueDate:dd.MM.yyyy} ({status})");
        }

        if (!string.IsNullOrEmpty(debt.Description))
            sb.AppendLine($"📝 {debt.Description}");

        sb.AppendLine($"\n🗓 Создан: {debt.CreatedAt:dd.MM.yyyy}");

        await CommandHelpers.SafeEditMessageAsync(bot, chatId, msgId, sb.ToString(),
            DebtKeyboards.Detail(debtId, isTheyOwe), ct, callbackQueryId);
    }

    // История платежей
    public async Task ShowHistoryAsync(ITelegramBotClient bot, long chatId, long userId, int debtId, int msgId, CancellationToken ct, string? callbackQueryId = null)
    {
        var debt = await debtService.GetByIdAsync(userId, debtId, ct);
        if (debt == null) return;

        var payments = await debtService.GetPaymentsAsync(debtId, ct);
        var isTheyOwe = debt.Type == DebtType.TheyOwe;

        var sb = new StringBuilder();
        sb.AppendLine($"📜 *История платежей: {debt.PersonName}*\n");
        sb.AppendLine($"💰 Изначально: *{debt.Amount:N0}* TJS");
        var paid = debt.Amount - debt.RemainingAmount;
        var percent = debt.Amount > 0 ? (paid / debt.Amount) * 100 : 0;
        sb.AppendLine($"📊 Погашено: *{paid:N0}* TJS ({percent:N0}%)\n");

        if (!payments.Any())
        {
            sb.AppendLine("_Платежей пока нет._");
        }
        else
        {
            sb.AppendLine("*Платежи:*");
            sb.AppendLine("━━━━━━━━━━━━");
            foreach (var p in payments.OrderBy(p => p.PaidAt))
            {
                sb.AppendLine($"📅 {p.PaidAt:dd.MM.yyyy}");
                sb.AppendLine($"└ +{p.Amount:N0} TJS");
            }
            sb.AppendLine("━━━━━━━━━━━━");
        }

        sb.AppendLine($"\n💰 Осталось: *{debt.RemainingAmount:N0}* TJS");

        await CommandHelpers.SafeEditMessageAsync(bot, chatId, msgId, sb.ToString(),
            DebtKeyboards.History(debtId, isTheyOwe), ct, callbackQueryId);
    }

    // После создания
    public async Task ShowAfterCreateAsync(ITelegramBotClient bot, long chatId, Domain.Entities.Debt debt, bool addedToBalance, CancellationToken ct)
    {
        var isTheyOwe = debt.Type == DebtType.TheyOwe;
        var typeIcon = isTheyOwe ? "📥" : "📤";
        var typeLabel = isTheyOwe ? "вам должен" : "вы должны";

        var sb = new StringBuilder();
        sb.AppendLine("✅ *Новый долг успешно создан!*\n");
        sb.AppendLine($"{typeIcon} *{debt.PersonName}* {typeLabel}");
        sb.AppendLine($"💰 *{debt.Amount:N0}* TJS");

        if (debt.DueDate.HasValue)
            sb.AppendLine($"📅 До: {debt.DueDate:dd.MM.yyyy}");
        if (!string.IsNullOrEmpty(debt.Description))
            sb.AppendLine($"📝 {debt.Description}");

        if (addedToBalance && !isTheyOwe)
            sb.AppendLine($"\n💳 +{debt.Amount:N0} TJS добавлено к балансу");

        await bot.SendTextMessageAsync(chatId, sb.ToString(),
            ParseMode.Markdown, replyMarkup: DebtKeyboards.AfterCreate(), cancellationToken: ct);
    }

    // === ДЕЙСТВИЯ ===

    // Записать платёж
    public async Task<bool> RecordPaymentAsync(ITelegramBotClient bot, long chatId, long userId, int debtId, decimal amount, CancellationToken ct)
    {
        var debt = await debtService.GetByIdAsync(userId, debtId, ct);
        if (debt == null) return false;

        var isTheyOwe = debt.Type == DebtType.TheyOwe;
        var account = await accountService.GetUserAccountAsync(userId, ct);

        // Транзакция
        int? txnId = null;
        if (isTheyOwe)
        {
            // Мне вернули → доход
            var cat = await EnsureCategoryAsync(userId, IncomeCategoryName, TransactionType.Income, ct);
            if (cat != null && account != null)
            {
                var txn = await transactionService.ProcessTransactionAsync(userId, cat.Id, amount, TransactionType.Income, $"← {debt.PersonName}", false, null, ct);
                txnId = txn?.Id;
            }
        }
        else
        {
            // Я плачу → расход
            if (account == null || account.Balance < amount)
            {
                await bot.SendTextMessageAsync(chatId, "❌ Недостаточно средств!", replyMarkup: DebtKeyboards.Cancel(), cancellationToken: ct);
                return false;
            }
            var cat = await EnsureCategoryAsync(userId, ExpenseCategoryName, TransactionType.Expense, ct);
            if (cat != null)
            {
                var txn = await transactionService.ProcessTransactionAsync(userId, cat.Id, amount, TransactionType.Expense, $"→ {debt.PersonName}", false, null, ct);
                txnId = txn?.Id;
            }
        }

        // Записать платёж
        var (updatedDebt, payment) = await debtService.RecordPaymentAsync(userId, debtId, amount, txnId, ct);
        if (updatedDebt == null) return false;

        // Результат
        var sb = new StringBuilder();
        var sign = isTheyOwe ? "+" : "-";

        if (updatedDebt.IsPaid)
        {
            sb.AppendLine("🎉 *ДОЛГ ЗАКРЫТ!*\n");
            sb.AppendLine($"✅ *{debt.PersonName}* полностью {(isTheyOwe ? "вернул" : "погашен")}!");
            sb.AppendLine($"\n💵 {(isTheyOwe ? "Получено" : "Оплачено")}: *{sign}{amount:N0}* TJS");
            sb.AppendLine($"📊 Всего: *{debt.Amount:N0}* TJS");
            if (isTheyOwe)
                sb.AppendLine($"\n💳 +{amount:N0} TJS → ваш баланс");
            else
                sb.AppendLine($"\n💳 -{amount:N0} TJS с баланса");
            sb.AppendLine("\n🎊 Поздравляем!");

            await bot.SendTextMessageAsync(chatId, sb.ToString(),
                ParseMode.Markdown, replyMarkup: DebtKeyboards.AfterFullPayment(), cancellationToken: ct);
        }
        else
        {
            var percent = debt.Amount > 0 ? ((debt.Amount - updatedDebt.RemainingAmount) / debt.Amount) * 100 : 0;
            sb.AppendLine("✅ *Платёж записан!*\n");
            sb.AppendLine($"💵 {(isTheyOwe ? "Получено" : "Оплачено")}: *{sign}{amount:N0}* TJS");
            sb.AppendLine($"💰 Осталось: *{updatedDebt.RemainingAmount:N0}* TJS");
            sb.AppendLine($"📊 {BuildProgressBar(percent)} *{percent:N0}%* погашено");
            if (isTheyOwe)
                sb.AppendLine($"\n💳 +{amount:N0} TJS → ваш баланс");
            else
                sb.AppendLine($"\n💳 -{amount:N0} TJS с баланса");

            await bot.SendTextMessageAsync(chatId, sb.ToString(),
                ParseMode.Markdown, replyMarkup: DebtKeyboards.AfterPayment(debtId, isTheyOwe), cancellationToken: ct);
        }
        return true;
    }

    // Удалить
    public async Task DeleteAsync(ITelegramBotClient bot, long chatId, long userId, int debtId, int msgId, CancellationToken ct, string? callbackQueryId = null)
    {
        var debt = await debtService.GetByIdAsync(userId, debtId, ct);
        if (debt == null) return;

        await debtService.DeleteAsync(userId, debtId, ct);
        await CommandHelpers.SafeEditMessageAsync(bot, chatId, msgId,
            $"✅ Долг «{debt.PersonName}» удалён.",
            DebtKeyboards.AfterCreate(), ct, callbackQueryId);
    }

    // === ХЕЛПЕРЫ ===

    private async Task<Domain.Entities.Category?> EnsureCategoryAsync(long userId, string name, TransactionType type, CancellationToken ct)
    {
        var cats = await categoryService.GetUserCategoriesAsync(userId, ct);
        var cat = cats.FirstOrDefault(c => c.Name == name && c.Type == type);
        if (cat == null)
        {
            await categoryService.CreateAsync(userId, name, type, null, ct);
            cats = await categoryService.GetUserCategoriesAsync(userId, ct);
            cat = cats.FirstOrDefault(c => c.Name == name && c.Type == type);
        }
        return cat;
    }

    private static string BuildProgressBar(decimal percent)
    {
        var filled = Math.Clamp((int)(percent / 10), 0, 10);
        return "[" + new string('▓', filled) + new string('░', 10 - filled) + "]";
    }
}
