using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class RegularPaymentService(DataContext context) : IRegularPaymentService
{
    // Все платежи пользователя
    public async Task<IReadOnlyList<RegularPayment>> GetUserPaymentsAsync(long userId, CancellationToken ct = default)
    {
        return await context.RegularPayments.AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.UserId == userId && !p.IsDeleted)
            .OrderBy(p => p.IsPaused)
            .ThenBy(p => p.NextDueDate ?? DateTimeOffset.MaxValue)
            .ToListAsync(ct);
    }

    // Активные (не на паузе)
    public async Task<IReadOnlyList<RegularPayment>> GetActiveAsync(long userId, CancellationToken ct = default)
    {
        return await context.RegularPayments.AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.UserId == userId && !p.IsPaused && !p.IsDeleted)
            .OrderBy(p => p.NextDueDate ?? DateTimeOffset.MaxValue)
            .ToListAsync(ct);
    }

    // Ожидающие (в пределах напоминания)
    public async Task<IReadOnlyList<RegularPayment>> GetDuePaymentsAsync(long userId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var payments = await context.RegularPayments.AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.UserId == userId && !p.IsPaused && !p.IsDeleted && p.NextDueDate != null)
            .ToListAsync(ct);
        return payments.Where(p => p.NextDueDate!.Value.AddDays(-p.ReminderDaysBefore) <= now).ToList();
    }

    // Просроченные
    public async Task<IReadOnlyList<RegularPayment>> GetOverduePaymentsAsync(long userId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await context.RegularPayments.AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.UserId == userId && !p.IsPaused && !p.IsDeleted && p.NextDueDate != null && p.NextDueDate < now)
            .OrderBy(p => p.NextDueDate)
            .ToListAsync(ct);
    }

    // По ID
    public async Task<RegularPayment?> GetByIdAsync(long userId, int paymentId, CancellationToken ct = default)
    {
        return await context.RegularPayments.AsNoTracking()
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == paymentId && p.UserId == userId && !p.IsDeleted, ct);
    }

    // По частоте
    public async Task<IReadOnlyList<RegularPayment>> GetByFrequencyAsync(long userId, PaymentFrequency frequency, CancellationToken ct = default)
    {
        return await context.RegularPayments.AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.UserId == userId && p.Frequency == frequency && !p.IsDeleted)
            .OrderBy(p => p.NextDueDate ?? DateTimeOffset.MaxValue)
            .ToListAsync(ct);
    }

    // История платежей
    public async Task<IReadOnlyList<RegularPaymentHistory>> GetHistoryAsync(int paymentId, CancellationToken ct = default)
    {
        return await context.RegularPaymentHistories.AsNoTracking()
            .Where(h => h.RegularPaymentId == paymentId)
            .OrderByDescending(h => h.PaidAt)
            .ToListAsync(ct);
    }

    // Сводка за месяц
    public async Task<(decimal totalMonth, int totalCount, decimal paidMonth, int paidCount, decimal pendingMonth, int pendingCount)> GetSummaryAsync(long userId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var startOfMonth = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, now.Offset);
        var endOfMonth = startOfMonth.AddMonths(1);

        var payments = await context.RegularPayments.AsNoTracking()
            .Where(p => p.UserId == userId && !p.IsDeleted && !p.IsPaused && p.Frequency == PaymentFrequency.Monthly)
            .ToListAsync(ct);

        var totalMonth = payments.Sum(p => p.Amount);
        var totalCount = payments.Count;

        // Оплаченные в этом месяце
        var paidThisMonth = payments.Where(p => p.LastPaidDate.HasValue && p.LastPaidDate >= startOfMonth && p.LastPaidDate < endOfMonth).ToList();
        var paidMonth = paidThisMonth.Sum(p => p.Amount);
        var paidCount = paidThisMonth.Count;

        // Ожидающие
        var pendingMonth = totalMonth - paidMonth;
        var pendingCount = totalCount - paidCount;

        return (totalMonth, totalCount, paidMonth, paidCount, pendingMonth, pendingCount);
    }

    // Создать
    public async Task<RegularPayment> CreateAsync(long userId, string name, decimal amount, PaymentFrequency frequency,
        int? categoryId = null, int? dayOfMonth = null, int reminderDaysBefore = 3, DateTimeOffset? startDate = null, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var nextDueDate = startDate ?? CalculateNextDueDate(now, frequency, dayOfMonth);

        var payment = new RegularPayment
        {
            UserId = userId,
            Name = name.Trim(),
            Amount = amount,
            Frequency = frequency,
            CategoryId = categoryId,
            DayOfMonth = dayOfMonth,
            ReminderDaysBefore = reminderDaysBefore,
            IsPaused = false,
            NextDueDate = nextDueDate,
            CreatedAt = now
        };
        context.RegularPayments.Add(payment);
        await context.SaveChangesAsync(ct);
        return payment;
    }

    // Обновить
    public async Task<RegularPayment?> UpdateAsync(long userId, int paymentId, string name, decimal amount, int? categoryId, CancellationToken ct = default)
    {
        var payment = await context.RegularPayments.FirstOrDefaultAsync(p => p.Id == paymentId && p.UserId == userId && !p.IsDeleted, ct);
        if (payment == null) return null;
        payment.Name = name.Trim();
        payment.Amount = amount;
        payment.CategoryId = categoryId;
        await context.SaveChangesAsync(ct);
        return payment;
    }

    // Обновить день
    public async Task<RegularPayment?> UpdateDayAsync(long userId, int paymentId, int dayOfMonth, CancellationToken ct = default)
    {
        var payment = await context.RegularPayments.FirstOrDefaultAsync(p => p.Id == paymentId && p.UserId == userId && !p.IsDeleted, ct);
        if (payment == null) return null;
        payment.DayOfMonth = dayOfMonth;
        payment.NextDueDate = CalculateNextDueDate(DateTimeOffset.UtcNow, payment.Frequency, dayOfMonth);
        await context.SaveChangesAsync(ct);
        return payment;
    }

    // Отметить оплаченным
    public async Task<(RegularPayment? payment, RegularPaymentHistory? history)> MarkAsPaidAsync(long userId, int paymentId, int? transactionId = null, CancellationToken ct = default)
    {
        var payment = await context.RegularPayments.FirstOrDefaultAsync(p => p.Id == paymentId && p.UserId == userId && !p.IsDeleted, ct);
        if (payment == null) return (null, null);

        var now = DateTimeOffset.UtcNow;
        var history = new RegularPaymentHistory
        {
            RegularPaymentId = paymentId,
            Amount = payment.Amount,
            PaidAt = now,
            TransactionId = transactionId
        };
        context.RegularPaymentHistories.Add(history);

        payment.LastPaidDate = now;
        payment.NextDueDate = CalculateNextDueDate(now, payment.Frequency, payment.DayOfMonth);
        await context.SaveChangesAsync(ct);
        return (payment, history);
    }

    // Приостановить/возобновить
    public async Task<RegularPayment?> SetPausedAsync(long userId, int paymentId, bool isPaused, CancellationToken ct = default)
    {
        var payment = await context.RegularPayments.FirstOrDefaultAsync(p => p.Id == paymentId && p.UserId == userId && !p.IsDeleted, ct);
        if (payment == null) return null;
        payment.IsPaused = isPaused;
        await context.SaveChangesAsync(ct);
        return payment;
    }

    // Удалить
    public async Task<bool> DeleteAsync(long userId, int paymentId, CancellationToken ct = default)
    {
        var payment = await context.RegularPayments.FirstOrDefaultAsync(p => p.Id == paymentId && p.UserId == userId && !p.IsDeleted, ct);
        if (payment == null) return false;
        payment.IsDeleted = true;
        payment.DeletedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(ct);
        return true;
    }

    // Хелперы
    private static DateTimeOffset CalculateNextDueDate(DateTimeOffset from, PaymentFrequency frequency, int? dayOfMonth)
    {
        return frequency switch
        {
            PaymentFrequency.Daily => from.AddDays(1),
            PaymentFrequency.Weekly => from.AddDays(7),
            PaymentFrequency.Monthly => CalculateNextMonthlyDate(from, dayOfMonth ?? from.Day),
            PaymentFrequency.Yearly => from.AddYears(1),
            _ => from.AddMonths(1)
        };
    }

    private static DateTimeOffset CalculateNextMonthlyDate(DateTimeOffset from, int dayOfMonth)
    {
        var nextMonth = from.AddMonths(1);
        var daysInMonth = DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month);
        var day = Math.Min(dayOfMonth, daysInMonth);
        return new DateTimeOffset(nextMonth.Year, nextMonth.Month, day, 0, 0, 0, from.Offset);
    }
}

