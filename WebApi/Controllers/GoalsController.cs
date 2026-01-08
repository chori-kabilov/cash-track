using Microsoft.AspNetCore.Mvc;
using Infrastructure.Services;
using Infrastructure.Mappers;
using Domain.DTOs;
using Swashbuckle.AspNetCore.Annotations;

namespace WebApi.Controllers;

// Контроллер целей
[ApiController]
[Route("api/goals")]
[SwaggerTag("Управление целями накопления")]
public class GoalsController(IGoalService goalService) : ControllerBase
{
    // === READ ===

    [HttpGet("user/{userId}")]
    [SwaggerOperation(Summary = "Все активные цели", Description = "Не завершённые")]
    public async Task<ActionResult<IEnumerable<GoalDto>>> GetByUser(long userId)
    {
        var goals = await goalService.GetUserGoalsAsync(userId);
        return Ok(goals.Select(GoalMapper.ToDto));
    }

    [HttpGet("user/{userId}/completed")]
    [SwaggerOperation(Summary = "Завершённые цели", Description = "История достигнутых целей")]
    public async Task<ActionResult<IEnumerable<GoalDto>>> GetCompleted(long userId)
    {
        var goals = await goalService.GetCompletedAsync(userId);
        return Ok(goals.Select(GoalMapper.ToDto));
    }

    [HttpGet("user/{userId}/active")]
    [SwaggerOperation(Summary = "Активная цель", Description = "Текущая приоритетная")]
    public async Task<ActionResult<GoalDto>> GetActive(long userId)
    {
        var g = await goalService.GetActiveGoalAsync(userId);
        return g != null ? Ok(GoalMapper.ToDto(g)) : NotFound(new { Message = "Нет активной цели" });
    }

    [HttpGet("{goalId}/user/{userId}")]
    [SwaggerOperation(Summary = "По ID")]
    public async Task<ActionResult<GoalDto>> GetById(long userId, int goalId)
    {
        var g = await goalService.GetByIdAsync(userId, goalId);
        return g != null ? Ok(GoalMapper.ToDto(g)) : NotFound();
    }

    // === CREATE ===

    [HttpPost("user/{userId}")]
    [SwaggerOperation(Summary = "Создать цель")]
    public async Task<ActionResult<GoalDto>> Create(
        long userId,
        [FromQuery] string name,
        [FromQuery] decimal targetAmount,
        [FromQuery] DateTimeOffset? deadline = null)
    {
        var g = await goalService.CreateAsync(userId, name, targetAmount, deadline);
        return Ok(GoalMapper.ToDto(g));
    }

    // === UPDATE ===

    [HttpPut("{goalId}/user/{userId}")]
    [SwaggerOperation(Summary = "Обновить цель", Description = "Имя, сумма, дедлайн")]
    public async Task<ActionResult<GoalDto>> Update(
        long userId,
        int goalId,
        [FromQuery] string name,
        [FromQuery] decimal targetAmount,
        [FromQuery] DateTimeOffset? deadline = null)
    {
        var g = await goalService.UpdateAsync(userId, goalId, name, targetAmount, deadline);
        return g != null ? Ok(GoalMapper.ToDto(g)) : NotFound();
    }

    [HttpPost("{goalId}/user/{userId}/deposit")]
    [SwaggerOperation(Summary = "Пополнить", Description = "Добавить к накоплению")]
    public async Task<ActionResult<GoalDto>> Deposit(long userId, int goalId, [FromQuery] decimal amount)
    {
        if (amount <= 0) return BadRequest(new { Error = "Сумма должна быть > 0" });
        var g = await goalService.AddFundsAsync(userId, goalId, amount);
        return g != null ? Ok(GoalMapper.ToDto(g)) : NotFound();
    }

    [HttpPost("{goalId}/user/{userId}/withdraw")]
    [SwaggerOperation(Summary = "Снять", Description = "Снять с накопления")]
    public async Task<ActionResult<GoalDto>> Withdraw(long userId, int goalId, [FromQuery] decimal amount)
    {
        if (amount <= 0) return BadRequest(new { Error = "Сумма должна быть > 0" });
        var g = await goalService.WithdrawAsync(userId, goalId, amount);
        return g != null ? Ok(GoalMapper.ToDto(g)) : NotFound(new { Error = "Недостаточно средств" });
    }

    [HttpPut("{goalId}/user/{userId}/activate")]
    [SwaggerOperation(Summary = "Активировать", Description = "Сделать приоритетной")]
    public async Task<ActionResult<GoalDto>> Activate(long userId, int goalId)
    {
        var g = await goalService.SetActiveAsync(userId, goalId);
        return g != null ? Ok(GoalMapper.ToDto(g)) : NotFound();
    }

    [HttpPut("{goalId}/user/{userId}/complete")]
    [SwaggerOperation(Summary = "Завершить", Description = "Отметить как достигнутую")]
    public async Task<ActionResult<GoalDto>> Complete(long userId, int goalId)
    {
        var g = await goalService.CompleteAsync(userId, goalId);
        return g != null ? Ok(GoalMapper.ToDto(g)) : NotFound();
    }

    // === DELETE ===

    [HttpDelete("{goalId}/user/{userId}")]
    [SwaggerOperation(Summary = "Удалить", Description = "Soft delete")]
    public async Task<ActionResult> Delete(long userId, int goalId)
    {
        var result = await goalService.DeleteAsync(userId, goalId);
        return result ? Ok(new { Message = "Цель удалена" }) : NotFound();
    }
}
