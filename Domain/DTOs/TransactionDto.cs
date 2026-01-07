using Domain.Enums;

namespace Domain.DTOs;

// DTO транзакции
public record TransactionDto(
    int Id,
    decimal Amount,
    TransactionType Type,
    string? Description,
    bool IsImpulsive,
    DateTimeOffset Date,
    CategoryDto? Category
);

// DTO для статистики
public record TransactionStatsDto(
    decimal TotalIncome,
    decimal TotalExpense,
    decimal Balance,
    int TransactionCount,
    IEnumerable<CategoryExpenseDto> TopExpenses
);
