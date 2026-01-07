using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class RegularPaymentService(DataContext context) : IRegularPaymentService
{
    public async Task<IReadOnlyList<RegularPayment>> GetUserPaymentsAsync(long userId, CancellationToken cancellationToken = default)
    {
        return await context.RegularPayments
            .AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.UserId == userId)
            .OrderBy(p => p.NextDueDate ?? DateTimeOffset.MaxValue)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RegularPayment>> GetDuePaymentsAsync(long userId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await context.RegularPayments
            .AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.UserId == userId && !p.IsPaused && p.NextDueDate != null)
            .ToListAsync(cancellationToken)
            .ContinueWith(t => t.Result
                .Where(p => p.NextDueDate!.Value.AddDays(-p.ReminderDaysBefore) <= now)
                .ToList() as IReadOnlyList<RegularPayment>, cancellationToken);
    }

    public async Task<IReadOnlyList<RegularPayment>> GetOverduePaymentsAsync(long userId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await context.RegularPayments
            .AsNoTracking()
            .Include(p => p.Category)
            .Where(p => p.UserId == userId && !p.IsPaused && p.NextDueDate != null && p.NextDueDate < now)
            .OrderBy(p => p.NextDueDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<RegularPayment?> GetByIdAsync(long userId, int paymentId, CancellationToken cancellationToken = default)
    {
        return await context.RegularPayments
            .AsNoTracking()
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == paymentId && p.UserId == userId, cancellationToken);
    }

    public async Task<RegularPayment> CreateAsync(long userId, string name, decimal amount, PaymentFrequency frequency,
        int? categoryId = null, int? dayOfMonth = null, int reminderDaysBefore = 3, DateTimeOffset? startDate = null,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        // If startDate is provided, use it as NextDueDate. Otherwise calculate from now.
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
        await context.SaveChangesAsync(cancellationToken);
        return payment;
    }

    public async Task<RegularPayment?> MarkAsPaidAsync(long userId, int paymentId, CancellationToken cancellationToken = default)
    {
        var payment = await context.RegularPayments.FirstOrDefaultAsync(p => p.Id == paymentId && p.UserId == userId, cancellationToken);
        if (payment == null)
            return null;

        var now = DateTimeOffset.UtcNow;
        payment.LastPaidDate = now;
        payment.NextDueDate = CalculateNextDueDate(now, payment.Frequency, payment.DayOfMonth);

        await context.SaveChangesAsync(cancellationToken);
        return payment;
    }

    public async Task<RegularPayment?> SetPausedAsync(long userId, int paymentId, bool isPaused, CancellationToken cancellationToken = default)
    {
        var payment = await context.RegularPayments.FirstOrDefaultAsync(p => p.Id == paymentId && p.UserId == userId, cancellationToken);
        if (payment == null)
            return null;

        payment.IsPaused = isPaused;
        await context.SaveChangesAsync(cancellationToken);
        return payment;
    }

    public async Task<bool> DeleteAsync(long userId, int paymentId, CancellationToken cancellationToken = default)
    {
        var payment = await context.RegularPayments.FirstOrDefaultAsync(p => p.Id == paymentId && p.UserId == userId, cancellationToken);
        if (payment == null)
            return false;

        payment.IsDeleted = true;
        payment.DeletedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

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
