using Domain.Entities;
using Domain.Enums;

namespace Infrastructure.Services;

public interface IDebtService
{
    /// <summary>
    /// Get all debts for a user.
    /// </summary>
    Task<IReadOnlyList<Debt>> GetUserDebtsAsync(long userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get unpaid debts.
    /// </summary>
    Task<IReadOnlyList<Debt>> GetUnpaidDebtsAsync(long userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get overdue debts.
    /// </summary>
    Task<IReadOnlyList<Debt>> GetOverdueDebtsAsync(long userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a debt by ID.
    /// </summary>
    Task<Debt?> GetByIdAsync(long userId, int debtId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new debt.
    /// </summary>
    Task<Debt> CreateAsync(long userId, string personName, decimal amount, DebtType type, 
        string? description = null, DateTimeOffset? dueDate = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Make a partial payment on a debt.
    /// </summary>
    Task<Debt?> MakePaymentAsync(long userId, int debtId, decimal amount, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark debt as fully paid.
    /// </summary>
    Task<Debt?> MarkAsPaidAsync(long userId, int debtId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a debt.
    /// </summary>
    Task<bool> DeleteAsync(long userId, int debtId, CancellationToken cancellationToken = default);
}
