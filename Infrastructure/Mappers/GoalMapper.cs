using Domain.DTOs;
using Domain.Entities;

namespace Infrastructure.Mappers;

// Маппер для Goal
public static class GoalMapper
{
    public static GoalDto ToDto(Goal g)
    {
        var remaining = g.TargetAmount - g.CurrentAmount;
        var progress = g.TargetAmount > 0 
            ? Math.Round((double)(g.CurrentAmount / g.TargetAmount) * 100, 1) 
            : 0;

        return new GoalDto(
            g.Id,
            g.Name,
            g.TargetAmount,
            g.CurrentAmount,
            remaining,
            progress,
            g.Deadline,
            g.IsActive,
            g.IsCompleted,
            g.CreatedAt
        );
    }
}
