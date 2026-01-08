using Microsoft.AspNetCore.Mvc;
using Infrastructure.Services;
using Infrastructure.Mappers;
using Domain.DTOs;
using Swashbuckle.AspNetCore.Annotations;

namespace WebApi.Controllers;

// Контроллер лимитов
[ApiController]
[Route("api/limits")]
[SwaggerTag("Управление лимитами расходов")]
public class LimitsController(ILimitService limitService) : ControllerBase
{
    // === READ ===

    [HttpGet("user/{userId}")]
    [SwaggerOperation(Summary = "Все лимиты")]
    public async Task<ActionResult<IEnumerable<LimitDto>>> GetByUser(long userId)
    {
        var limits = await limitService.GetUserLimitsAsync(userId);
        return Ok(limits.Select(LimitMapper.ToDto));
    }

    [HttpGet("user/{userId}/exceeded")]
    [SwaggerOperation(Summary = "Превышенные", Description = "SpentAmount >= Amount")]
    public async Task<ActionResult<IEnumerable<LimitDto>>> GetExceeded(long userId)
    {
        var limits = await limitService.GetExceededAsync(userId);
        return Ok(limits.Select(LimitMapper.ToDto));
    }

    [HttpGet("user/{userId}/blocked")]
    [SwaggerOperation(Summary = "Заблокированные")]
    public async Task<ActionResult<IEnumerable<LimitDto>>> GetBlocked(long userId)
    {
        var limits = await limitService.GetBlockedAsync(userId);
        return Ok(limits.Select(LimitMapper.ToDto));
    }

    [HttpGet("{limitId}/user/{userId}")]
    [SwaggerOperation(Summary = "По ID")]
    public async Task<ActionResult<LimitDto>> GetById(long userId, int limitId)
    {
        var l = await limitService.GetByIdAsync(userId, limitId);
        return l != null ? Ok(LimitMapper.ToDto(l)) : NotFound();
    }

    [HttpGet("category/{categoryId}/user/{userId}")]
    [SwaggerOperation(Summary = "По категории")]
    public async Task<ActionResult<LimitDto>> GetByCategory(long userId, int categoryId)
    {
        var l = await limitService.GetByCategoryAsync(userId, categoryId);
        return l != null ? Ok(LimitMapper.ToDto(l)) : NotFound();
    }

    [HttpGet("category/{categoryId}/user/{userId}/blocked")]
    [SwaggerOperation(Summary = "Проверка блокировки категории")]
    public async Task<ActionResult<object>> IsBlocked(long userId, int categoryId)
    {
        var isBlocked = await limitService.IsCategoryBlockedAsync(userId, categoryId);
        return Ok(new { CategoryId = categoryId, IsBlocked = isBlocked });
    }

    [HttpGet("user/{userId}/summary")]
    [SwaggerOperation(Summary = "Сводка")]
    public async Task<ActionResult<LimitSummaryDto>> GetSummary(long userId)
    {
        var limits = await limitService.GetUserLimitsAsync(userId);
        
        return Ok(new LimitSummaryDto(
            limits.Count,
            limits.Sum(l => l.Amount),
            limits.Sum(l => l.SpentAmount),
            limits.Sum(l => l.Amount - l.SpentAmount),
            limits.Count(l => l.SpentAmount >= l.Amount),
            limits.Count(l => l.IsBlocked)
        ));
    }

    // === CREATE ===

    [HttpPost("user/{userId}")]
    [SwaggerOperation(Summary = "Создать лимит")]
    public async Task<ActionResult<LimitDto>> Create(long userId, [FromQuery] int categoryId, [FromQuery] decimal amount)
    {
        var l = await limitService.CreateAsync(userId, categoryId, amount);
        return Ok(LimitMapper.ToDto(l));
    }

    // === UPDATE ===

    [HttpPut("{limitId}/user/{userId}")]
    [SwaggerOperation(Summary = "Обновить сумму")]
    public async Task<ActionResult<LimitDto>> UpdateAmount(long userId, int limitId, [FromQuery] decimal amount)
    {
        var l = await limitService.UpdateAmountAsync(userId, limitId, amount);
        return l != null ? Ok(LimitMapper.ToDto(l)) : NotFound();
    }

    [HttpPut("{limitId}/user/{userId}/block")]
    [SwaggerOperation(Summary = "Заблокировать", Description = "Вручную заблокировать лимит")]
    public async Task<ActionResult<LimitDto>> Block(long userId, int limitId, [FromQuery] DateTimeOffset? until = null)
    {
        var l = await limitService.BlockAsync(userId, limitId, until);
        return l != null ? Ok(LimitMapper.ToDto(l)) : NotFound();
    }

    [HttpPost("category/{categoryId}/user/{userId}/spend")]
    [SwaggerOperation(Summary = "Добавить расход", Description = "Увеличивает SpentAmount")]
    public async Task<ActionResult<object>> AddSpending(long userId, int categoryId, [FromQuery] decimal amount)
    {
        var (limit, warningLevel) = await limitService.AddSpendingAsync(userId, categoryId, amount);
        return Ok(new { Limit = limit != null ? LimitMapper.ToDto(limit) : null, WarningLevel = warningLevel });
    }

    [HttpPost("user/{userId}/reset")]
    [SwaggerOperation(Summary = "Сбросить месячные лимиты")]
    public async Task<ActionResult> ResetMonthly(long userId)
    {
        await limitService.ResetMonthlyLimitsAsync(userId);
        return Ok(new { Message = "Лимиты сброшены" });
    }

    [HttpPost("user/{userId}/unblock-expired")]
    [SwaggerOperation(Summary = "Разблокировать истёкшие")]
    public async Task<ActionResult> UnblockExpired(long userId)
    {
        await limitService.UnblockExpiredCategoriesAsync(userId);
        return Ok(new { Message = "Разблокировано" });
    }

    // === DELETE ===

    [HttpDelete("{limitId}/user/{userId}")]
    [SwaggerOperation(Summary = "Удалить", Description = "Soft delete")]
    public async Task<ActionResult> Delete(long userId, int limitId)
    {
        var result = await limitService.DeleteAsync(userId, limitId);
        return result ? Ok(new { Message = "Лимит удалён" }) : NotFound();
    }
}
