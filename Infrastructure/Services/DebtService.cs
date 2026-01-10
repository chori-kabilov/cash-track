using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class DebtService(DataContext context) : IDebtService
{
    // Все долги пользователя
    public async Task<IReadOnlyList<Debt>> GetUserDebtsAsync(long userId, CancellationToken ct = default)
    {
        return await context.Debts.AsNoTracking()
            .Where(d => d.UserId == userId && !d.IsDeleted)
            .OrderBy(d => d.IsPaid)
            .ThenBy(d => d.DueDate ?? DateTimeOffset.MaxValue)
            .ThenByDescending(d => d.CreatedAt)
            .ToListAsync(ct);
    }

    // Неоплаченные
    public async Task<IReadOnlyList<Debt>> GetUnpaidDebtsAsync(long userId, CancellationToken ct = default)
    {
        return await context.Debts.AsNoTracking()
            .Where(d => d.UserId == userId && !d.IsPaid && !d.IsDeleted)
            .OrderBy(d => d.DueDate ?? DateTimeOffset.MaxValue)
            .ToListAsync(ct);
    }

    // Просроченные
    public async Task<IReadOnlyList<Debt>> GetOverdueDebtsAsync(long userId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await context.Debts.AsNoTracking()
            .Where(d => d.UserId == userId && !d.IsPaid && !d.IsDeleted && d.DueDate != null && d.DueDate < now)
            .OrderBy(d => d.DueDate)
            .ToListAsync(ct);
    }

    // По ID
    public async Task<Debt?> GetByIdAsync(long userId, int debtId, CancellationToken ct = default)
    {
        return await context.Debts.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == debtId && d.UserId == userId && !d.IsDeleted, ct);
    }

    // История платежей
    public async Task<IReadOnlyList<DebtPayment>> GetPaymentsAsync(int debtId, CancellationToken ct = default)
    {
        return await context.DebtPayments.AsNoTracking()
            .Where(p => p.DebtId == debtId)
            .OrderByDescending(p => p.PaidAt)
            .ToListAsync(ct);
    }

    // Сводка (для дашборда)
    public async Task<(decimal theyOwe, int theyOweCount, decimal iOwe, int iOweCount)> GetSummaryAsync(long userId, CancellationToken ct = default)
    {
        var debts = await context.Debts.AsNoTracking()
            .Where(d => d.UserId == userId && !d.IsDeleted && !d.IsPaid)
            .ToListAsync(ct);

        var theyOwe = debts.Where(d => d.Type == DebtType.TheyOwe).Sum(d => d.RemainingAmount);
        var theyOweCount = debts.Count(d => d.Type == DebtType.TheyOwe);
        var iOwe = debts.Where(d => d.Type == DebtType.IOwe).Sum(d => d.RemainingAmount);
        var iOweCount = debts.Count(d => d.Type == DebtType.IOwe);

        return (theyOwe, theyOweCount, iOwe, iOweCount);
    }

    // Оплаченные
    public async Task<IReadOnlyList<Debt>> GetPaidDebtsAsync(long userId, CancellationToken ct = default)
    {
        return await context.Debts.AsNoTracking()
            .Where(d => d.UserId == userId && d.IsPaid && !d.IsDeleted)
            .OrderByDescending(d => d.PaidAt)
            .ToListAsync(ct);
    }

    // По типу
    public async Task<IReadOnlyList<Debt>> GetByTypeAsync(long userId, DebtType type, CancellationToken ct = default)
    {
        return await context.Debts.AsNoTracking()
            .Where(d => d.UserId == userId && d.Type == type && !d.IsDeleted && !d.IsPaid)
            .OrderBy(d => d.DueDate ?? DateTimeOffset.MaxValue)
            .ToListAsync(ct);
    }

    // Создать
    public async Task<Debt> CreateAsync(long userId, string personName, decimal amount, DebtType type, string? description = null, DateTimeOffset? dueDate = null, CancellationToken ct = default)
    {
        var debt = new Debt
        {
            UserId = userId,
            PersonName = personName.Trim(),
            Amount = amount,
            RemainingAmount = amount,
            Type = type,
            Description = description?.Trim(),
            TakenDate = DateTimeOffset.UtcNow,
            DueDate = dueDate,
            IsPaid = false,
            CreatedAt = DateTimeOffset.UtcNow
        };
        context.Debts.Add(debt);
        await context.SaveChangesAsync(ct);
        return debt;
    }

    // Записать платёж
    public async Task<(Debt? debt, DebtPayment? payment)> RecordPaymentAsync(long userId, int debtId, decimal amount, int? transactionId = null, CancellationToken ct = default)
    {
        var debt = await context.Debts.FirstOrDefaultAsync(d => d.Id == debtId && d.UserId == userId && !d.IsDeleted, ct);
        if (debt == null) return (null, null);

        var payment = new DebtPayment
        {
            DebtId = debtId,
            Amount = amount,
            PaidAt = DateTimeOffset.UtcNow,
            TransactionId = transactionId
        };
        context.DebtPayments.Add(payment);

        debt.RemainingAmount -= amount;
        if (debt.RemainingAmount <= 0)
        {
            debt.RemainingAmount = 0;
            debt.IsPaid = true;
            debt.PaidAt = DateTimeOffset.UtcNow;
        }
        await context.SaveChangesAsync(ct);
        return (debt, payment);
    }

    // Пометить полностью оплаченным
    public async Task<Debt?> MarkAsPaidAsync(long userId, int debtId, CancellationToken ct = default)
    {
        var debt = await context.Debts.FirstOrDefaultAsync(d => d.Id == debtId && d.UserId == userId && !d.IsDeleted, ct);
        if (debt == null) return null;
        debt.RemainingAmount = 0;
        debt.IsPaid = true;
        debt.PaidAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(ct);
        return debt;
    }

    // Обновить
    public async Task<Debt?> UpdateAsync(long userId, int debtId, string personName, string? description, DateTimeOffset? dueDate, CancellationToken ct = default)
    {
        var debt = await context.Debts.FirstOrDefaultAsync(d => d.Id == debtId && d.UserId == userId && !d.IsDeleted, ct);
        if (debt == null) return null;
        debt.PersonName = personName.Trim();
        debt.Description = description?.Trim();
        debt.DueDate = dueDate;
        await context.SaveChangesAsync(ct);
        return debt;
    }

    // Удалить
    public async Task<bool> DeleteAsync(long userId, int debtId, CancellationToken ct = default)
    {
        var debt = await context.Debts.FirstOrDefaultAsync(d => d.Id == debtId && d.UserId == userId && !d.IsDeleted, ct);
        if (debt == null) return false;
        debt.IsDeleted = true;
        debt.DeletedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(ct);
        return true;
    }
}


