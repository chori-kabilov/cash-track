using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class BudgetService(DataContext context) : IBudgetService
{
    public async Task<Budget?> GetBudgetAsync(long userId, int categoryId, CancellationToken cancellationToken = default)
    {
        return await context.Budgets
            .AsNoTracking()
            .Include(b => b.Category)
            .FirstOrDefaultAsync(b => b.UserId == userId && b.CategoryId == categoryId, cancellationToken);
    }

    public async Task<IReadOnlyList<Budget>> GetUserBudgetsAsync(long userId, CancellationToken cancellationToken = default)
    {
        return await context.Budgets
            .AsNoTracking()
            .Include(b => b.Category)
            .Where(b => b.UserId == userId)
            .OrderBy(b => b.Category.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Budget> SetBudgetAsync(long userId, int categoryId, decimal monthlyLimit, CancellationToken cancellationToken = default)
    {
        var budget = await context.Budgets.FirstOrDefaultAsync(b => b.UserId == userId && b.CategoryId == categoryId, cancellationToken);
        if (budget == null)
        {
            budget = new Budget
            {
                UserId = userId,
                CategoryId = categoryId,
                MonthlyLimit = monthlyLimit,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            context.Budgets.Add(budget);
        }
        else
        {
            budget.MonthlyLimit = monthlyLimit;
            budget.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);
        return budget;
    }

    public async Task<bool> RemoveBudgetAsync(long userId, int categoryId, CancellationToken cancellationToken = default)
    {
        var budget = await context.Budgets.FirstOrDefaultAsync(b => b.UserId == userId && b.CategoryId == categoryId, cancellationToken);
        if (budget == null)
            return false;

        context.Budgets.Remove(budget);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
