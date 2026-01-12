using System.Text;
using Console.Bot.Keyboards;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Services;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace Console.Commands;

// Команда для регулярных платежей (дашборд + все экраны)
public class RegularPaymentCommand(
    IRegularPaymentService regularService,
    IAccountService accountService,
    ITransactionService transactionService,
    ICategoryService categoryService)
{
    private const string ExpenseCategoryName = "→ Регулярный платёж";

    // Точка входа
    public async Task ExecuteAsync(ITelegramBotClient bot, long chatId, long userId, CancellationToken ct, int? messageId = null, string? callbackQueryId = null)
    {
        if (messageId.HasValue)
            await ShowDashboardAsync(bot, chatId, userId, messageId.Value, ct, callbackQueryId);
        else
        {
            var msg = await bot.SendTextMessageAsync(chatId, "🔄 Загрузка...", cancellationToken: ct);
            await ShowDashboardAsync(bot, chatId, userId, msg.MessageId, ct, callbackQueryId);
        }
    }

    // === ЭКРАНЫ ===

    // Дашборд
    public async Task ShowDashboardAsync(ITelegramBotClient bot, long chatId, long userId, int msgId, CancellationToken ct, string? callbackQueryId = null)
    {
        var payments = await regularService.GetUserPaymentsAsync(userId, ct);
        var activePayments = payments.Where(p => !p.IsPaused).ToList();

        if (!payments.Any())
        {
            await CommandHelpers.SafeEditMessageAsync(bot, chatId, msgId,
                "🔄 *Регулярные платежи*\n\nУ вас нет регулярных платежей.\nДобавьте первый!",
                RegularKeyboards.Empty(), ct, callbackQueryId);
            return;
        }

        var (totalMonth, totalCount, paidMonth, paidCount, pendingMonth, pendingCount) = 
            await regularService.GetSummaryAsync(userId, ct);
        var overdue = await regularService.GetOverduePaymentsAsync(userId, ct);
        var due = await regularService.GetDuePaymentsAsync(userId, ct);

        var sb = new StringBuilder();
        sb.AppendLine("🔄 *Регулярные платежи*\n");
        sb.AppendLine("📊 *В этом месяце:*");
        sb.AppendLine($"💰 Всего: *{totalMonth:N0}* TJS ({totalCount} платежей)");
        sb.AppendLine($"✅ Оплачено: *{paidMonth:N0}* TJS ({paidCount})");
        sb.AppendLine($"⏳ Ожидает: *{pendingMonth:N0}* TJS ({pendingCount})");

        if (overdue.Any() || due.Any())
        {
            sb.AppendLine("\n⚠️ *Требуют внимания:*");
            foreach (var p in overdue.Take(2))
                sb.AppendLine($"🔴 {p.Name} — {p.Amount:N0} TJS (просрочен)");
            foreach (var p in due.Where(d => !overdue.Any(o => o.Id == d.Id)).Take(2))
                sb.AppendLine($"🟡 {p.Name} — {p.Amount:N0} TJS ({p.NextDueDate:dd.MM})");
        }

        await CommandHelpers.SafeEditMessageAsync(bot, chatId, msgId, sb.ToString(),
            RegularKeyboards.Dashboard(), ct, callbackQueryId);
    }

    // Список платежей
    public async Task ShowListAsync(ITelegramBotClient bot, long chatId, long userId, int msgId, int page, CancellationToken ct, string? callbackQueryId = null)
    {
        var payments = await regularService.GetUserPaymentsAsync(userId, ct);

        if (!payments.Any())
        {
            await ShowDashboardAsync(bot, chatId, userId, msgId, ct, callbackQueryId);
            return;
        }

        int pageSize = 5;
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)payments.Count / pageSize));
        if (page < 0) page = 0;
        if (page >= totalPages) page = totalPages - 1;

        var sb = new StringBuilder();
        sb.AppendLine("📋 *Ваши платежи*");
        sb.AppendLine($"*Страница {page + 1} из {totalPages}*\n");

        var pagePayments = payments.Skip(page * pageSize).Take(pageSize).ToList();
        var startNum = page * pageSize + 1;

        foreach (var (p, idx) in pagePayments.Select((p, i) => (p, i)))
        {
            var num = startNum + idx;
            var statusIcon = p.IsPaused ? "⏸" : (p.NextDueDate < DateTimeOffset.UtcNow ? "🔴" : "⏳");
            var freq = GetFrequencyText(p.Frequency, p.DayOfMonth);
            sb.AppendLine($"{num}. {statusIcon} *{p.Name}* — {p.Amount:N0} TJS");
            sb.AppendLine($"   🔄 {freq}");
        }

        sb.AppendLine("\n👇 *Введите номер для деталей:*");

        await CommandHelpers.SafeEditMessageAsync(bot, chatId, msgId, sb.ToString(),
            RegularKeyboards.List(page, totalPages), ct, callbackQueryId);
    }

    // Детали платежа
    public async Task ShowDetailAsync(ITelegramBotClient bot, long chatId, long userId, int paymentId, int msgId, CancellationToken ct, string? callbackQueryId = null)
    {
        var payment = await regularService.GetByIdAsync(userId, paymentId, ct);
        if (payment == null) { await ShowDashboardAsync(bot, chatId, userId, msgId, ct, callbackQueryId); return; }

        var account = await accountService.GetUserAccountAsync(userId, ct);
        var hasEnough = account != null && account.Balance >= payment.Amount;

        var sb = new StringBuilder();
        var catName = payment.Category?.Name ?? "Без категории";
        var catEmoji = payment.Category?.Icon ?? "📂";
        var freq = GetFrequencyText(payment.Frequency, payment.DayOfMonth);

        sb.AppendLine($"📋 *{payment.Name}*\n");
        sb.AppendLine($"💰 Сумма: *{payment.Amount:N0}* TJS");
        sb.AppendLine($"🔄 {freq}");
        sb.AppendLine($"{catEmoji} Категория: {catName}");

        if (payment.IsPaused)
        {
            sb.AppendLine("\n⏸ *Статус:* Приостановлен");
        }
        else if (payment.NextDueDate.HasValue)
        {
            var days = (payment.NextDueDate.Value - DateTimeOffset.UtcNow).Days;
            var status = days < 0 ? $"просрочен на {-days} дн." : (days == 0 ? "сегодня!" : $"через {days} дн.");
            var statusIcon = days < 0 ? "🔴" : (days <= 3 ? "🟡" : "🟢");
            sb.AppendLine($"\n{statusIcon} *Следующий:* {payment.NextDueDate:dd.MM.yyyy} ({status})");
        }

        if (!hasEnough && !payment.IsPaused)
        {
            sb.AppendLine($"\n⚠️ *Недостаточно средств!*");
            sb.AppendLine($"💳 Баланс: {account?.Balance ?? 0:N0} TJS");
            sb.AppendLine($"📉 Не хватает: *{payment.Amount - (account?.Balance ?? 0):N0}* TJS");
        }
        else if (!payment.IsPaused)
        {
            sb.AppendLine($"\n💳 Баланс: {account?.Balance ?? 0:N0} TJS ✅");
        }

        await CommandHelpers.SafeEditMessageAsync(bot, chatId, msgId, sb.ToString(),
            RegularKeyboards.Detail(paymentId, payment.IsPaused, hasEnough), ct, callbackQueryId);
    }

    // История
    public async Task ShowHistoryAsync(ITelegramBotClient bot, long chatId, long userId, int paymentId, int msgId, CancellationToken ct, string? callbackQueryId = null)
    {
        var payment = await regularService.GetByIdAsync(userId, paymentId, ct);
        if (payment == null) return;

        var history = await regularService.GetHistoryAsync(paymentId, ct);

        var sb = new StringBuilder();
        sb.AppendLine($"📜 *История: {payment.Name}*\n");
        sb.AppendLine($"💰 {payment.Amount:N0} TJS / {GetFrequencyShort(payment.Frequency)}");

        if (!history.Any())
        {
            sb.AppendLine("\n_Платежей пока нет._");
        }
        else
        {
            sb.AppendLine("\n*Последние платежи:*");
            sb.AppendLine("━━━━━━━━━━━━");
            foreach (var h in history.Take(10))
                sb.AppendLine($"✅ {h.PaidAt:dd.MM.yyyy} — {h.Amount:N0} TJS");
            sb.AppendLine("━━━━━━━━━━━━");
            sb.AppendLine($"\n📊 Оплачено: {history.Count} раз");
            sb.AppendLine($"💰 Всего: {history.Sum(h => h.Amount):N0} TJS");
        }

        await CommandHelpers.SafeEditMessageAsync(bot, chatId, msgId, sb.ToString(),
            RegularKeyboards.History(paymentId), ct, callbackQueryId);
    }

    // После создания
    public async Task ShowAfterCreateAsync(ITelegramBotClient bot, long chatId, RegularPayment payment, CancellationToken ct)
    {
        var freq = GetFrequencyText(payment.Frequency, payment.DayOfMonth);
        var catName = payment.Category?.Name ?? "Без категории";
        var catEmoji = payment.Category?.Icon ?? "📂";

        var sb = new StringBuilder();
        sb.AppendLine("✅ *Платёж создан!*\n");
        sb.AppendLine($"📋 *{payment.Name}*");
        sb.AppendLine($"💰 {payment.Amount:N0} TJS");
        sb.AppendLine($"🔄 {freq}");
        sb.AppendLine($"{catEmoji} Категория: {catName}");
        sb.AppendLine($"\n📅 Следующий платёж: {payment.NextDueDate:dd.MM.yyyy}");

        await bot.SendTextMessageAsync(chatId, sb.ToString(),
            ParseMode.Markdown, replyMarkup: RegularKeyboards.AfterCreate(), cancellationToken: ct);
    }

    // === ДЕЙСТВИЯ ===

    // Отметить оплаченным
    public async Task<bool> MarkAsPaidAsync(ITelegramBotClient bot, long chatId, long userId, int paymentId, CancellationToken ct)
    {
        var payment = await regularService.GetByIdAsync(userId, paymentId, ct);
        if (payment == null) return false;

        var account = await accountService.GetUserAccountAsync(userId, ct);
        if (account == null || account.Balance < payment.Amount)
        {
            await bot.SendTextMessageAsync(chatId, 
                $"⚠️ *Недостаточно средств!*\n\n💳 Баланс: {account?.Balance ?? 0:N0} TJS\n📉 Нужно: {payment.Amount:N0} TJS",
                ParseMode.Markdown, replyMarkup: RegularKeyboards.Cancel(), cancellationToken: ct);
            return false;
        }

        // Транзакция
        int? txnId = null;
        var cat = await EnsureCategoryAsync(userId, ExpenseCategoryName, TransactionType.Expense, ct);
        if (cat != null)
        {
            var txn = await transactionService.ProcessTransactionAsync(userId, cat.Id, payment.Amount, 
                TransactionType.Expense, $"→ {payment.Name}", false, null, ct);
            txnId = txn?.Id;
        }

        // Отметить оплаченным
        var (updatedPayment, history) = await regularService.MarkAsPaidAsync(userId, paymentId, txnId, ct);
        if (updatedPayment == null) return false;

        var freq = GetFrequencyText(updatedPayment.Frequency, updatedPayment.DayOfMonth);
        var sb = new StringBuilder();
        sb.AppendLine("✅ *Оплачено!*\n");
        sb.AppendLine($"📋 *{payment.Name}* — {payment.Amount:N0} TJS");
        sb.AppendLine($"💳 -{payment.Amount:N0} TJS с баланса");
        sb.AppendLine($"📊 Баланс: *{(account.Balance - payment.Amount):N0}* TJS");
        sb.AppendLine($"\n📅 Следующий платёж: {updatedPayment.NextDueDate:dd.MM.yyyy}");

        await bot.SendTextMessageAsync(chatId, sb.ToString(),
            ParseMode.Markdown, replyMarkup: RegularKeyboards.AfterPay(paymentId), cancellationToken: ct);
        return true;
    }

    // Удалить
    public async Task DeleteAsync(ITelegramBotClient bot, long chatId, long userId, int paymentId, int msgId, CancellationToken ct, string? callbackQueryId = null)
    {
        var payment = await regularService.GetByIdAsync(userId, paymentId, ct);
        if (payment == null) return;

        await regularService.DeleteAsync(userId, paymentId, ct);
        await CommandHelpers.SafeEditMessageAsync(bot, chatId, msgId,
            $"✅ Удалено.\n\n📋 {payment.Name} — удалён",
            RegularKeyboards.AfterCreate(), ct, callbackQueryId);
    }

    // === ХЕЛПЕРЫ ===

    private async Task<Category?> EnsureCategoryAsync(long userId, string name, TransactionType type, CancellationToken ct)
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

    private static string GetFrequencyText(PaymentFrequency freq, int? day)
    {
        return freq switch
        {
            PaymentFrequency.Daily => "Ежедневно",
            PaymentFrequency.Weekly => "Еженедельно",
            PaymentFrequency.Monthly => day.HasValue ? $"Ежемесячно, {day} числа" : "Ежемесячно",
            PaymentFrequency.Yearly => "Ежегодно",
            _ => "Другое"
        };
    }

    private static string GetFrequencyShort(PaymentFrequency freq)
    {
        return freq switch
        {
            PaymentFrequency.Daily => "день",
            PaymentFrequency.Weekly => "неделю",
            PaymentFrequency.Monthly => "месяц",
            PaymentFrequency.Yearly => "год",
            _ => "период"
        };
    }
}
