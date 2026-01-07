using Domain.Entities;
using Domain.Enums;

namespace Infrastructure.Services;

public interface IDebtService
{
    Task<IReadOnlyList<Debt>> GetUserDebtsAsync(long userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Debt>> GetUnpaidDebtsAsync(long userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Debt>> GetOverdueDebtsAsync(long userId, CancellationToken cancellationToken = default);
    Task<Debt?> GetByIdAsync(long userId, int debtId, CancellationToken cancellationToken = default);
    Task<Debt> CreateAsync(long userId, string personName, decimal amount, DebtType type, 
        string? description = null, DateTimeOffset? dueDate = null, CancellationToken cancellationToken = default);
    Task<Debt?> MakePaymentAsync(long userId, int debtId, decimal amount, CancellationToken cancellationToken = default);
    Task<Debt?> MarkAsPaidAsync(long userId, int debtId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(long userId, int debtId, CancellationToken cancellationToken = default);
}
