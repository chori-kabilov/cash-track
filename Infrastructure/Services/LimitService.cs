using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class LimitService(DataContext context) : ILimitService
{
    public async Task<IReadOnlyList<Limit>> GetUserLimitsAsync(long userId, CancellationToken cancellationToken = default)
    {
        return await context.Limits
            .AsNoTracking()
            .Include(l => l.Category)
            .Where(l => l.UserId == userId)
            .OrderBy(l => l.Category.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Limit?> GetByCategoryAsync(long userId, int categoryId, CancellationToken cancellationToken = default)
    {
        return await context.Limits
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.UserId == userId && l.CategoryId == categoryId, cancellationToken);
    }

    public async Task<Limit?> GetByIdAsync(long userId, int limitId, CancellationToken cancellationToken = default)
    {
        return await context.Limits
            .AsNoTracking()
            .Include(l => l.Category)
            .FirstOrDefaultAsync(l => l.Id == limitId && l.UserId == userId, cancellationToken);
    }

    public async Task<Limit> CreateAsync(long userId, int categoryId, decimal amount, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, now.Offset);

        var limit = new Limit
        {
            UserId = userId,
            CategoryId = categoryId,
            Amount = amount,
            SpentAmount = 0,
            Period = "month",
            PeriodStart = monthStart,
            IsBlocked = false,
            LastWarningLevel = 0,
            CreatedAt = now
        };

        context.Limits.Add(limit);
        await context.SaveChangesAsync(cancellationToken);
        return limit;
    }

    public async Task<(Limit? Limit, int WarningLevel)> AddSpendingAsync(long userId, int categoryId, decimal amount, CancellationToken cancellationToken = default)
    {
        var limit = await context.Limits.FirstOrDefaultAsync(l => l.UserId == userId && l.CategoryId == categoryId, cancellationToken);
        if (limit == null)
            return (null, 0);

        limit.SpentAmount += amount;

        var percentage = limit.Amount > 0 ? (limit.SpentAmount / limit.Amount) * 100 : 0;
        int newWarningLevel = 0;

        if (percentage >= 100 && limit.LastWarningLevel < 100)
        {
            newWarningLevel = 100;
            limit.IsBlocked = true;
            limit.BlockedUntil = DateTimeOffset.UtcNow.AddDays(1);
        }
        else if (percentage >= 80 && limit.LastWarningLevel < 80)
        {
            newWarningLevel = 80;
        }
        else if (percentage >= 50 && limit.LastWarningLevel < 50)
        {
            newWarningLevel = 50;
        }

        if (newWarningLevel > limit.LastWarningLevel)
        {
            limit.LastWarningLevel = newWarningLevel;
        }

        await context.SaveChangesAsync(cancellationToken);
        return (limit, newWarningLevel);
    }

    public async Task<bool> IsCategoryBlockedAsync(long userId, int categoryId, CancellationToken cancellationToken = default)
    {
        var limit = await context.Limits
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.UserId == userId && l.CategoryId == categoryId, cancellationToken);

        if (limit == null)
            return false;

        if (!limit.IsBlocked)
            return false;

        // Check if block has expired
        if (limit.BlockedUntil.HasValue && limit.BlockedUntil.Value <= DateTimeOffset.UtcNow)
            return false;

        return true;
    }

    public async Task ResetMonthlyLimitsAsync(long userId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, now.Offset);

        var limits = await context.Limits
            .Where(l => l.UserId == userId && l.Period == "month" && l.PeriodStart < monthStart)
            .ToListAsync(cancellationToken);

        foreach (var limit in limits)
        {
            limit.SpentAmount = 0;
            limit.PeriodStart = monthStart;
            limit.IsBlocked = false;
            limit.BlockedUntil = null;
            limit.LastWarningLevel = 0;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UnblockExpiredCategoriesAsync(long userId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var expiredBlocks = await context.Limits
            .Where(l => l.UserId == userId && l.IsBlocked && l.BlockedUntil != null && l.BlockedUntil <= now)
            .ToListAsync(cancellationToken);

        foreach (var limit in expiredBlocks)
        {
            limit.IsBlocked = false;
            limit.BlockedUntil = null;
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(long userId, int limitId, CancellationToken cancellationToken = default)
    {
        var limit = await context.Limits.FirstOrDefaultAsync(l => l.Id == limitId && l.UserId == userId, cancellationToken);
        if (limit == null)
            return false;

        context.Limits.Remove(limit);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
