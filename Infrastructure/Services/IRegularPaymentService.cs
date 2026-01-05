using Domain.Entities;
using Domain.Enums;

namespace Infrastructure.Services;

public interface IRegularPaymentService
{
    /// <summary>
    /// Get all regular payments for a user.
    /// </summary>
    Task<IReadOnlyList<RegularPayment>> GetUserPaymentsAsync(long userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get payments due soon (within reminder period).
    /// </summary>
    Task<IReadOnlyList<RegularPayment>> GetDuePaymentsAsync(long userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get overdue payments.
    /// </summary>
    Task<IReadOnlyList<RegularPayment>> GetOverduePaymentsAsync(long userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a payment by ID.
    /// </summary>
    Task<RegularPayment?> GetByIdAsync(long userId, int paymentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create a new regular payment.
    /// </summary>
    Task<RegularPayment> CreateAsync(long userId, string name, decimal amount, PaymentFrequency frequency,
        int? categoryId = null, int? dayOfMonth = null, int reminderDaysBefore = 3, DateTimeOffset? startDate = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark payment as paid and calculate next due date.
    /// </summary>
    Task<RegularPayment?> MarkAsPaidAsync(long userId, int paymentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pause/unpause a payment.
    /// </summary>
    Task<RegularPayment?> SetPausedAsync(long userId, int paymentId, bool isPaused, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete a payment.
    /// </summary>
    Task<bool> DeleteAsync(long userId, int paymentId, CancellationToken cancellationToken = default);
}
