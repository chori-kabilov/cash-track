namespace Domain.DTOs;

// DTO лимита
public record LimitDto(
    int Id,
    decimal Amount,
    decimal SpentAmount,
    decimal Remaining,
    double PercentUsed,
    string Status,
    bool IsBlocked,
    CategoryDto? Category
);

// DTO сводки по лимитам
public record LimitSummaryDto(
    int TotalLimits,
    decimal TotalBudget,
    decimal TotalSpent,
    decimal Remaining,
    int ExceededCount,
    int BlockedCount
);
