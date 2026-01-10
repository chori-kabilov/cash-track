using Domain.Entities;
using Domain.Enums;

namespace Infrastructure.Services;

public interface IDebtService
{
    // READ
    Task<IReadOnlyList<Debt>> GetUserDebtsAsync(long userId, CancellationToken ct = default);
    Task<IReadOnlyList<Debt>> GetUnpaidDebtsAsync(long userId, CancellationToken ct = default);
    Task<IReadOnlyList<Debt>> GetPaidDebtsAsync(long userId, CancellationToken ct = default);
    Task<IReadOnlyList<Debt>> GetOverdueDebtsAsync(long userId, CancellationToken ct = default);
    Task<IReadOnlyList<Debt>> GetByTypeAsync(long userId, DebtType type, CancellationToken ct = default);
    Task<Debt?> GetByIdAsync(long userId, int debtId, CancellationToken ct = default);
    Task<IReadOnlyList<DebtPayment>> GetPaymentsAsync(int debtId, CancellationToken ct = default);
    
    // Summary
    Task<(decimal theyOwe, int theyOweCount, decimal iOwe, int iOweCount)> GetSummaryAsync(long userId, CancellationToken ct = default);
    
    // CREATE
    Task<Debt> CreateAsync(long userId, string personName, decimal amount, DebtType type, 
        string? description = null, DateTimeOffset? dueDate = null, CancellationToken ct = default);
    
    // UPDATE
    Task<Debt?> UpdateAsync(long userId, int debtId, string personName, string? description, DateTimeOffset? dueDate, CancellationToken ct = default);
    Task<(Debt? debt, DebtPayment? payment)> RecordPaymentAsync(long userId, int debtId, decimal amount, int? transactionId = null, CancellationToken ct = default);
    Task<Debt?> MarkAsPaidAsync(long userId, int debtId, CancellationToken ct = default);
    
    // DELETE
    Task<bool> DeleteAsync(long userId, int debtId, CancellationToken ct = default);
}
