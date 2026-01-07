using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class GoalService(DataContext context) : IGoalService
{
    public async Task<IReadOnlyList<Goal>> GetUserGoalsAsync(long userId, CancellationToken cancellationToken = default)
    {
        return await context.Goals
            .AsNoTracking()
            .Where(g => g.UserId == userId && !g.IsCompleted)
            .OrderByDescending(g => g.IsActive)
            .ThenBy(g => g.Priority)
            .ThenBy(g => g.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Goal?> GetActiveGoalAsync(long userId, CancellationToken cancellationToken = default)
    {
        return await context.Goals
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.UserId == userId && g.IsActive && !g.IsCompleted, cancellationToken);
    }

    public async Task<Goal?> GetByIdAsync(long userId, int goalId, CancellationToken cancellationToken = default)
    {
        return await context.Goals
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == goalId && g.UserId == userId, cancellationToken);
    }

    public async Task<Goal> CreateAsync(long userId, string name, decimal targetAmount, DateTimeOffset? deadline = null, CancellationToken cancellationToken = default)
    {
        // If this is the first goal, make it active
        var hasActiveGoal = await context.Goals.AnyAsync(g => g.UserId == userId && g.IsActive && !g.IsCompleted, cancellationToken);

        var goal = new Goal
        {
            UserId = userId,
            Name = name.Trim(),
            TargetAmount = targetAmount,
            CurrentAmount = 0,
            Deadline = deadline,
            Priority = 1,
            IsActive = !hasActiveGoal,
            IsCompleted = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        context.Goals.Add(goal);
        await context.SaveChangesAsync(cancellationToken);
        return goal;
    }

    public async Task<Goal?> AddFundsAsync(long userId, int goalId, decimal amount, CancellationToken cancellationToken = default)
    {
        var goal = await context.Goals.FirstOrDefaultAsync(g => g.Id == goalId && g.UserId == userId, cancellationToken);
        if (goal == null)
            return null;

        goal.CurrentAmount += amount;

        // Check if goal is now complete
        if (goal.CurrentAmount >= goal.TargetAmount)
        {
            goal.IsCompleted = true;
            goal.CompletedAt = DateTimeOffset.UtcNow;
            goal.IsActive = false;
        }

        await context.SaveChangesAsync(cancellationToken);
        return goal;
    }

    public async Task<Goal?> SetActiveAsync(long userId, int goalId, CancellationToken cancellationToken = default)
    {
        // Deactivate all other goals
        var activeGoals = await context.Goals
            .Where(g => g.UserId == userId && g.IsActive && !g.IsCompleted)
            .ToListAsync(cancellationToken);

        foreach (var g in activeGoals)
        {
            g.IsActive = false;
        }

        // Activate the selected goal
        var goal = await context.Goals.FirstOrDefaultAsync(g => g.Id == goalId && g.UserId == userId, cancellationToken);
        if (goal == null)
            return null;

        goal.IsActive = true;
        await context.SaveChangesAsync(cancellationToken);
        return goal;
    }

    public async Task<Goal?> CompleteAsync(long userId, int goalId, CancellationToken cancellationToken = default)
    {
        var goal = await context.Goals.FirstOrDefaultAsync(g => g.Id == goalId && g.UserId == userId, cancellationToken);
        if (goal == null)
            return null;

        goal.IsCompleted = true;
        goal.CompletedAt = DateTimeOffset.UtcNow;
        goal.IsActive = false;

        await context.SaveChangesAsync(cancellationToken);
        return goal;
    }

    public async Task<bool> DeleteAsync(long userId, int goalId, CancellationToken cancellationToken = default)
    {
        var goal = await context.Goals.FirstOrDefaultAsync(g => g.Id == goalId && g.UserId == userId, cancellationToken);
        if (goal == null)
            return false;

        goal.IsDeleted = true;
        goal.DeletedAt = DateTimeOffset.UtcNow;

        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
