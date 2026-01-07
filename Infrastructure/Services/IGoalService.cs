using Domain.Entities;

namespace Infrastructure.Services;

public interface IGoalService
{
    Task<IReadOnlyList<Goal>> GetUserGoalsAsync(long userId, CancellationToken cancellationToken = default);
    Task<Goal?> GetActiveGoalAsync(long userId, CancellationToken cancellationToken = default);
    Task<Goal?> GetByIdAsync(long userId, int goalId, CancellationToken cancellationToken = default);
    Task<Goal> CreateAsync(long userId, string name, decimal targetAmount, DateTimeOffset? deadline = null, CancellationToken cancellationToken = default);
    Task<Goal?> AddFundsAsync(long userId, int goalId, decimal amount, CancellationToken cancellationToken = default);
    Task<Goal?> SetActiveAsync(long userId, int goalId, CancellationToken cancellationToken = default);
    Task<Goal?> CompleteAsync(long userId, int goalId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(long userId, int goalId, CancellationToken cancellationToken = default);
}
