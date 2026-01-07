namespace Domain.DTOs;

// DTO цели
public record GoalDto(
    int Id,
    string Name,
    decimal TargetAmount,
    decimal CurrentAmount,
    decimal Remaining,
    double ProgressPercent,
    DateTimeOffset? Deadline,
    bool IsActive,
    bool IsCompleted,
    DateTimeOffset CreatedAt
);
