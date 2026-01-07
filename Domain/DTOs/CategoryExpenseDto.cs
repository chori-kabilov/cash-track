namespace Domain.DTOs;

// DTO расхода по категории
public record CategoryExpenseDto(
    string Name,
    string? Icon,
    decimal Amount
);
