using Domain.Entities;

namespace Infrastructure.Services;

public interface IBudgetService
{
    Task<Budget?> GetBudgetAsync(long userId, int categoryId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Budget>> GetUserBudgetsAsync(long userId, CancellationToken cancellationToken = default);
    Task<Budget> SetBudgetAsync(long userId, int categoryId, decimal monthlyLimit, CancellationToken cancellationToken = default);
    Task<bool> RemoveBudgetAsync(long userId, int categoryId, CancellationToken cancellationToken = default);
}
