using Domain.Entities;
using Domain.Enums;

namespace Infrastructure.Services;

public interface IRegularPaymentService
{    
    Task<IReadOnlyList<RegularPayment>> GetUserPaymentsAsync(long userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RegularPayment>> GetDuePaymentsAsync(long userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RegularPayment>> GetOverduePaymentsAsync(long userId, CancellationToken cancellationToken = default);
    Task<RegularPayment?> GetByIdAsync(long userId, int paymentId, CancellationToken cancellationToken = default);
    Task<RegularPayment> CreateAsync(long userId, string name, decimal amount, PaymentFrequency frequency,
        int? categoryId = null, int? dayOfMonth = null, int reminderDaysBefore = 3, DateTimeOffset? startDate = null,
        CancellationToken cancellationToken = default);
    Task<RegularPayment?> MarkAsPaidAsync(long userId, int paymentId, CancellationToken cancellationToken = default);
    Task<RegularPayment?> SetPausedAsync(long userId, int paymentId, bool isPaused, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(long userId, int paymentId, CancellationToken cancellationToken = default);
}
