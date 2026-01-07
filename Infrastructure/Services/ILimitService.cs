using Domain.Entities;

namespace Infrastructure.Services;

public interface ILimitService
{    
    Task<IReadOnlyList<Limit>> GetUserLimitsAsync(long userId, CancellationToken cancellationToken = default);
    Task<Limit?> GetByCategoryAsync(long userId, int categoryId, CancellationToken cancellationToken = default);
    Task<Limit?> GetByIdAsync(long userId, int limitId, CancellationToken cancellationToken = default);
    Task<Limit> CreateAsync(long userId, int categoryId, decimal amount, CancellationToken cancellationToken = default);
    /// </summary>
    Task<(Limit? Limit, int WarningLevel)> AddSpendingAsync(long userId, int categoryId, decimal amount, CancellationToken cancellationToken = default);
    Task<bool> IsCategoryBlockedAsync(long userId, int categoryId, CancellationToken cancellationToken = default);
    Task ResetMonthlyLimitsAsync(long userId, CancellationToken cancellationToken = default);
    Task UnblockExpiredCategoriesAsync(long userId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(long userId, int limitId, CancellationToken cancellationToken = default);
}
