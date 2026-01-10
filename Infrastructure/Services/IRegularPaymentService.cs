using Domain.Entities;
using Domain.Enums;

namespace Infrastructure.Services;

public interface IRegularPaymentService
{    
    // READ
    Task<IReadOnlyList<RegularPayment>> GetUserPaymentsAsync(long userId, CancellationToken ct = default);
    Task<IReadOnlyList<RegularPayment>> GetActiveAsync(long userId, CancellationToken ct = default);
    Task<IReadOnlyList<RegularPayment>> GetByFrequencyAsync(long userId, PaymentFrequency frequency, CancellationToken ct = default);
    Task<IReadOnlyList<RegularPayment>> GetDuePaymentsAsync(long userId, CancellationToken ct = default);
    Task<IReadOnlyList<RegularPayment>> GetOverduePaymentsAsync(long userId, CancellationToken ct = default);
    Task<RegularPayment?> GetByIdAsync(long userId, int paymentId, CancellationToken ct = default);
    Task<IReadOnlyList<RegularPaymentHistory>> GetHistoryAsync(int paymentId, CancellationToken ct = default);
    
    // Summary
    Task<(decimal totalMonth, int totalCount, decimal paidMonth, int paidCount, decimal pendingMonth, int pendingCount)> GetSummaryAsync(long userId, CancellationToken ct = default);
    
    // CREATE
    Task<RegularPayment> CreateAsync(long userId, string name, decimal amount, PaymentFrequency frequency,
        int? categoryId = null, int? dayOfMonth = null, int reminderDaysBefore = 3, DateTimeOffset? startDate = null,
        CancellationToken ct = default);
    
    // UPDATE
    Task<RegularPayment?> UpdateAsync(long userId, int paymentId, string name, decimal amount, int? categoryId, CancellationToken ct = default);
    Task<RegularPayment?> UpdateDayAsync(long userId, int paymentId, int dayOfMonth, CancellationToken ct = default);
    Task<(RegularPayment? payment, RegularPaymentHistory? history)> MarkAsPaidAsync(long userId, int paymentId, int? transactionId = null, CancellationToken ct = default);
    Task<RegularPayment?> SetPausedAsync(long userId, int paymentId, bool isPaused, CancellationToken ct = default);
    
    // DELETE
    Task<bool> DeleteAsync(long userId, int paymentId, CancellationToken ct = default);
}
