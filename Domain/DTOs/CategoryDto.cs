using Domain.Enums;

namespace Domain.DTOs;

// DTO категории
public record CategoryDto(
    int Id,
    string Name,
    string? Icon,
    TransactionType? Type,
    Priority Priority,
    bool IsActive
);
