using Domain.Entities;

namespace Infrastructure.Services;

public interface IGoalService
{
    /// <summary>
    /// Get all goals for a user.
    /// </summary>
    Task<IReadOnlyList<Goal>> GetUserGoalsAsync(long userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the active goal (the one being saved for).
    /// </summary>
    Task<Goal?> GetActiveGoalAsync(long userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a goal by ID.
    /// </summary>
    Task<Goal?> GetByIdAsync(long userId, int goalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new goal.
    /// </summary>
    Task<Goal> CreateAsync(long userId, string name, decimal targetAmount, DateTimeOffset? deadline = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Add funds to a goal.
    /// </summary>
    Task<Goal?> AddFundsAsync(long userId, int goalId, decimal amount, CancellationToken cancellationToken = default);

    /// <summary>
    /// Set a goal as the active one.
    /// </summary>
    Task<Goal?> SetActiveAsync(long userId, int goalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark a goal as completed.
    /// </summary>
    Task<Goal?> CompleteAsync(long userId, int goalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a goal.
    /// </summary>
    Task<bool> DeleteAsync(long userId, int goalId, CancellationToken cancellationToken = default);
}
