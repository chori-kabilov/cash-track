using Domain.Entities;
using Domain.Enums;

namespace Infrastructure.Services;

public interface ITransactionService
{
    Task<Transaction> CreateTransactionAsync(
        int accountId, 
        int categoryId, 
        decimal amount, 
        TransactionType type, 
        string? description = null, 
        bool isImpulsive = false, 
        bool isDraft = false, 
        bool isError = false, 
        DateTimeOffset? date = null, 
        CancellationToken cancellationToken = default);

    Task<Transaction> ProcessTransactionAsync(
        long userId,
        int categoryId,
        decimal amount,
        TransactionType type,
        string? description = null,
        bool isImpulsive = false,
        DateTimeOffset? date = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Transaction>> GetUserTransactionsAsync(long userId, int limit = 10, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Transaction> Items, int TotalCount)> GetTransactionsPageAsync(long userId, int page, int pageSize, TransactionType? type = null, DateTimeOffset? fromDateUtc = null, string? search = null, CancellationToken cancellationToken = default);
    Task<Transaction?> GetLastTransactionAsync(long userId, CancellationToken cancellationToken = default);
    Task<decimal> GetTotalIncomeAsync(long userId, DateTimeOffset? fromDateUtc = null, CancellationToken cancellationToken = default);
    Task<decimal> GetTotalExpenseAsync(long userId, DateTimeOffset? fromDateUtc = null, CancellationToken cancellationToken = default);
    Task<decimal> GetCategoryExpenseAsync(long userId, int categoryId, DateTimeOffset fromDateUtc, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<int>> GetRecentCategoryIdsAsync(long userId, TransactionType type, int limit = 6, CancellationToken ct = default);
    Task<IReadOnlyList<(Category Category, decimal Amount)>> GetTopExpensesAsync(long userId, DateTimeOffset fromDate, int count, CancellationToken ct = default);
    
    // Новые методы
    Task<Transaction?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Transaction>> GetByCategoryAsync(long userId, int categoryId, int limit = 50, CancellationToken ct = default);
    Task<Transaction?> UpdateDescriptionAsync(int id, string? description, CancellationToken ct = default);
    Task<Transaction?> CancelAsync(int id, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}
