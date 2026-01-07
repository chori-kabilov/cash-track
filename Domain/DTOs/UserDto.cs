namespace Domain.DTOs;

// DTO пользователя
public record UserDto(
    long Id,
    string FirstName,
    string? LastName,
    string? Username,
    string Timezone,
    bool IsBalanceHidden,
    DateTimeOffset CreatedAt
);
