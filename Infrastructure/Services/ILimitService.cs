using Domain.Entities;

namespace Infrastructure.Services;

public interface ILimitService
{
    /// <summary>
    /// Get all limits for a user.
    /// </summary>
    Task<IReadOnlyList<Limit>> GetUserLimitsAsync(long userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get limit for a specific category.
    /// </summary>
    Task<Limit?> GetByCategoryAsync(long userId, int categoryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a limit by ID.
    /// </summary>
    Task<Limit?> GetByIdAsync(long userId, int limitId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new limit.
    /// </summary>
    Task<Limit> CreateAsync(long userId, int categoryId, decimal amount, CancellationToken cancellationToken = default);

    /// <summary>
    /// Update spent amount and check for warnings/blocking.
    /// Returns warning level (0, 50, 80, 100) if threshold crossed.
    /// </summary>
    Task<(Limit? Limit, int WarningLevel)> AddSpendingAsync(long userId, int categoryId, decimal amount, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if category is blocked.
    /// </summary>
    Task<bool> IsCategoryBlockedAsync(long userId, int categoryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reset monthly limits (call at start of new month).
    /// </summary>
    Task ResetMonthlyLimitsAsync(long userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unblock expired categories.
    /// </summary>
    Task UnblockExpiredCategoriesAsync(long userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a limit.
    /// </summary>
    Task<bool> DeleteAsync(long userId, int limitId, CancellationToken cancellationToken = default);
}
