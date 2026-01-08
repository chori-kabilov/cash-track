using Domain.Entities;

namespace Infrastructure.Services;

public interface ILimitService
{    
    // READ
    Task<IReadOnlyList<Limit>> GetUserLimitsAsync(long userId, CancellationToken ct = default);
    Task<IReadOnlyList<Limit>> GetExceededAsync(long userId, CancellationToken ct = default);
    Task<IReadOnlyList<Limit>> GetBlockedAsync(long userId, CancellationToken ct = default);
    Task<Limit?> GetByCategoryAsync(long userId, int categoryId, CancellationToken ct = default);
    Task<Limit?> GetByIdAsync(long userId, int limitId, CancellationToken ct = default);
    Task<bool> IsCategoryBlockedAsync(long userId, int categoryId, CancellationToken ct = default);
    
    // CREATE
    Task<Limit> CreateAsync(long userId, int categoryId, decimal amount, CancellationToken ct = default);
    
    // UPDATE
    Task<Limit?> UpdateAmountAsync(long userId, int limitId, decimal amount, CancellationToken ct = default);
    Task<Limit?> BlockAsync(long userId, int limitId, DateTimeOffset? until, CancellationToken ct = default);
    Task<(Limit? Limit, int WarningLevel)> AddSpendingAsync(long userId, int categoryId, decimal amount, CancellationToken ct = default);
    Task ResetMonthlyLimitsAsync(long userId, CancellationToken ct = default);
    Task UnblockExpiredCategoriesAsync(long userId, CancellationToken ct = default);
    
    // DELETE
    Task<bool> DeleteAsync(long userId, int limitId, CancellationToken ct = default);
}
