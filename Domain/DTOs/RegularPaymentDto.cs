using Domain.Enums;

namespace Domain.DTOs;

// DTO регулярного платежа
public record RegularPaymentDto(
    int Id,
    string Name,
    decimal Amount,
    PaymentFrequency Frequency,
    string FrequencyName,
    int? DayOfMonth,
    DateTimeOffset? NextDueDate,
    DateTimeOffset? LastPaidDate,
    bool IsPaused,
    bool IsOverdue,
    CategoryDto? Category
);

// DTO сводки
public record RegularPaymentSummaryDto(
    int TotalPayments,
    int ActivePayments,
    int PausedPayments,
    decimal MonthlyTotal,
    int DueNow
);
