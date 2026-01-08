using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class DebtService(DataContext context) : IDebtService
{
    public async Task<IReadOnlyList<Debt>> GetUserDebtsAsync(long userId, CancellationToken cancellationToken = default)
    {
        return await context.Debts
            .AsNoTracking()
            .Where(d => d.UserId == userId)
            .OrderBy(d => d.IsPaid)
            .ThenBy(d => d.DueDate ?? DateTimeOffset.MaxValue)
            .ThenByDescending(d => d.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Debt>> GetUnpaidDebtsAsync(long userId, CancellationToken cancellationToken = default)
    {
        return await context.Debts
            .AsNoTracking()
            .Where(d => d.UserId == userId && !d.IsPaid)
            .OrderBy(d => d.DueDate ?? DateTimeOffset.MaxValue)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Debt>> GetOverdueDebtsAsync(long userId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await context.Debts
            .AsNoTracking()
            .Where(d => d.UserId == userId && !d.IsPaid && d.DueDate != null && d.DueDate < now)
            .OrderBy(d => d.DueDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<Debt?> GetByIdAsync(long userId, int debtId, CancellationToken cancellationToken = default)
    {
        return await context.Debts
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == debtId && d.UserId == userId, cancellationToken);
    }

    public async Task<Debt> CreateAsync(long userId, string personName, decimal amount, DebtType type,
        string? description = null, DateTimeOffset? dueDate = null, CancellationToken cancellationToken = default)
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
        await context.SaveChangesAsync(cancellationToken);
        return debt;
    }

    public async Task<Debt?> MakePaymentAsync(long userId, int debtId, decimal amount, CancellationToken cancellationToken = default)
    {
        var debt = await context.Debts.FirstOrDefaultAsync(d => d.Id == debtId && d.UserId == userId, cancellationToken);
        if (debt == null)
            return null;

        debt.RemainingAmount -= amount;
        if (debt.RemainingAmount <= 0)
        {
            debt.RemainingAmount = 0;
            debt.IsPaid = true;
            debt.PaidAt = DateTimeOffset.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);
        return debt;
    }

    public async Task<Debt?> MarkAsPaidAsync(long userId, int debtId, CancellationToken cancellationToken = default)
    {
        var debt = await context.Debts.FirstOrDefaultAsync(d => d.Id == debtId && d.UserId == userId, cancellationToken);
        if (debt == null)
            return null;

        debt.RemainingAmount = 0;
        debt.IsPaid = true;
        debt.PaidAt = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
        return debt;
    }

    public async Task<bool> DeleteAsync(long userId, int debtId, CancellationToken cancellationToken = default)
    {
        var debt = await context.Debts.FirstOrDefaultAsync(d => d.Id == debtId && d.UserId == userId && !d.IsDeleted, cancellationToken);
        if (debt == null)
            return false;

        debt.IsDeleted = true;
        debt.DeletedAt = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
        return true;
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
            .Where(d => d.UserId == userId && d.Type == type && !d.IsDeleted)
            .OrderBy(d => d.IsPaid)
            .ThenBy(d => d.DueDate ?? DateTimeOffset.MaxValue)
            .ToListAsync(ct);
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
}

