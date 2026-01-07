using Domain.Enums;

namespace Domain.DTOs;

// DTO долга
public record DebtDto(
    int Id,
    string PersonName,
    decimal Amount,
    decimal RemainingAmount,
    DebtType Type,
    string TypeName,
    string? Description,
    DateTimeOffset TakenDate,
    DateTimeOffset? DueDate,
    bool IsPaid,
    bool IsOverdue
);

// DTO сводки по долгам
public record DebtSummaryDto(
    decimal TotalIOwe,
    decimal TotalTheyOwe,
    int CountIOwe,
    int CountTheyOwe,
    int OverdueCount
);
