using Domain.Entities;

namespace Infrastructure.Services;

public interface IGoalService
{
    // READ
    Task<IReadOnlyList<Goal>> GetUserGoalsAsync(long userId, CancellationToken ct = default);
    Task<IReadOnlyList<Goal>> GetCompletedAsync(long userId, CancellationToken ct = default);
    Task<Goal?> GetActiveGoalAsync(long userId, CancellationToken ct = default);
    Task<Goal?> GetByIdAsync(long userId, int goalId, CancellationToken ct = default);
    
    // CREATE
    Task<Goal> CreateAsync(long userId, string name, decimal targetAmount, DateTimeOffset? deadline = null, CancellationToken ct = default);
    
    // UPDATE
    Task<Goal?> UpdateAsync(long userId, int goalId, string name, decimal targetAmount, DateTimeOffset? deadline, CancellationToken ct = default);
    Task<Goal?> AddFundsAsync(long userId, int goalId, decimal amount, CancellationToken ct = default);
    Task<Goal?> WithdrawAsync(long userId, int goalId, decimal amount, CancellationToken ct = default);
    Task<Goal?> SetActiveAsync(long userId, int goalId, CancellationToken ct = default);
    Task<Goal?> CompleteAsync(long userId, int goalId, CancellationToken ct = default);
    
    // DELETE
    Task<bool> DeleteAsync(long userId, int goalId, CancellationToken ct = default);
}
