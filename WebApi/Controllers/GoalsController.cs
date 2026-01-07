using Microsoft.AspNetCore.Mvc;
using Infrastructure.Services;
using Domain.DTOs;

namespace WebApi.Controllers;

// Контроллер целей
[ApiController]
[Route("api/goals")]
public class GoalsController(IGoalService goalService) : ControllerBase
{
    // Маппинг в DTO
    private static GoalDto ToDto(Domain.Entities.Goal g) => new(
        g.Id,
        g.Name,
        g.TargetAmount,
        g.CurrentAmount,
        g.TargetAmount - g.CurrentAmount,
        g.TargetAmount > 0 ? Math.Round((double)(g.CurrentAmount / g.TargetAmount) * 100, 1) : 0,
        g.Deadline,
        g.IsActive,
        g.IsCompleted,
        g.CreatedAt
    );

    // Получить все цели
    [HttpGet("user/{userId}")]
    public async Task<ActionResult<IEnumerable<GoalDto>>> GetByUser(long userId)
    {
        var goals = await goalService.GetUserGoalsAsync(userId);
        return Ok(goals.Select(ToDto));
    }

    // Получить активную цель
    [HttpGet("user/{userId}/active")]
    public async Task<ActionResult<GoalDto>> GetActive(long userId)
    {
        var g = await goalService.GetActiveGoalAsync(userId);
        return g != null ? Ok(ToDto(g)) : NotFound(new { Message = "Нет активной цели" });
    }

    // Получить по ID
    [HttpGet("{goalId}/user/{userId}")]
    public async Task<ActionResult<GoalDto>> GetById(long userId, int goalId)
    {
        var g = await goalService.GetByIdAsync(userId, goalId);
        return g != null ? Ok(ToDto(g)) : NotFound();
    }

    // Создать цель
    [HttpPost("user/{userId}")]
    public async Task<ActionResult<GoalDto>> Create(long userId, [FromQuery] string name, [FromQuery] decimal targetAmount, [FromQuery] DateTimeOffset? deadline = null)
    {
        var g = await goalService.CreateAsync(userId, name, targetAmount, deadline);
        return Ok(ToDto(g));
    }

    // Пополнить цель
    [HttpPost("{goalId}/user/{userId}/deposit")]
    public async Task<ActionResult<GoalDto>> Deposit(long userId, int goalId, [FromQuery] decimal amount)
    {
        var g = await goalService.AddFundsAsync(userId, goalId, amount);
        return g != null ? Ok(ToDto(g)) : NotFound();
    }

    // Активировать
    [HttpPut("{goalId}/user/{userId}/activate")]
    public async Task<ActionResult<GoalDto>> Activate(long userId, int goalId)
    {
        var g = await goalService.SetActiveAsync(userId, goalId);
        return g != null ? Ok(ToDto(g)) : NotFound();
    }

    // Выполнить
    [HttpPut("{goalId}/user/{userId}/complete")]
    public async Task<ActionResult<GoalDto>> Complete(long userId, int goalId)
    {
        var g = await goalService.CompleteAsync(userId, goalId);
        return g != null ? Ok(ToDto(g)) : NotFound();
    }

    // Удалить
    [HttpDelete("{goalId}/user/{userId}")]
    public async Task<ActionResult> Delete(long userId, int goalId)
    {
        var result = await goalService.DeleteAsync(userId, goalId);
        return result ? Ok(new { Message = "Удалено" }) : NotFound();
    }
}
