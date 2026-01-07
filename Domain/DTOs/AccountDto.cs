namespace Domain.DTOs;

// DTO счёта
public record AccountDto(
    int Id,
    long UserId,
    string Name,
    decimal Balance,
    string Currency,
    DateTimeOffset UpdatedAt
);
