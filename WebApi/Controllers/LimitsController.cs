using Microsoft.AspNetCore.Mvc;
using Infrastructure.Services;
using Domain.DTOs;

namespace WebApi.Controllers;

// Контроллер лимитов
[ApiController]
[Route("api/limits")]
public class LimitsController(ILimitService limitService) : ControllerBase
{
    // Маппинг в DTO
    private static LimitDto ToDto(Domain.Entities.Limit l)
    {
        var percent = l.Amount > 0 ? Math.Round((double)(l.SpentAmount / l.Amount) * 100, 1) : 0;
        var status = percent >= 100 ? "Превышен" : percent >= 80 ? "Предупреждение" : percent >= 50 ? "Внимание" : "OK";
        
        return new LimitDto(
            l.Id,
            l.Amount,
            l.SpentAmount,
            l.Amount - l.SpentAmount,
            percent,
            status,
            l.IsBlocked,
            l.Category != null ? new CategoryDto(l.Category.Id, l.Category.Name, l.Category.Icon, l.Category.Type, l.Category.Priority, l.Category.IsActive) : null
        );
    }

    // Получить все
    [HttpGet("user/{userId}")]
    public async Task<ActionResult<IEnumerable<LimitDto>>> GetByUser(long userId)
    {
        var limits = await limitService.GetUserLimitsAsync(userId);
        return Ok(limits.Select(ToDto));
    }

    // По ID
    [HttpGet("{limitId}/user/{userId}")]
    public async Task<ActionResult<LimitDto>> GetById(long userId, int limitId)
    {
        var l = await limitService.GetByIdAsync(userId, limitId);
        return l != null ? Ok(ToDto(l)) : NotFound();
    }

    // По категории
    [HttpGet("category/{categoryId}/user/{userId}")]
    public async Task<ActionResult<LimitDto>> GetByCategory(long userId, int categoryId)
    {
        var l = await limitService.GetByCategoryAsync(userId, categoryId);
        return l != null ? Ok(ToDto(l)) : NotFound();
    }

    // Заблокирована ли
    [HttpGet("category/{categoryId}/user/{userId}/blocked")]
    public async Task<ActionResult<object>> IsBlocked(long userId, int categoryId)
    {
        var isBlocked = await limitService.IsCategoryBlockedAsync(userId, categoryId);
        return Ok(new { CategoryId = categoryId, IsBlocked = isBlocked });
    }

    // Сводка
    [HttpGet("user/{userId}/summary")]
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

    // Создать
    [HttpPost("user/{userId}")]
    public async Task<ActionResult<LimitDto>> Create(long userId, [FromQuery] int categoryId, [FromQuery] decimal amount)
    {
        var l = await limitService.CreateAsync(userId, categoryId, amount);
        return Ok(ToDto(l));
    }

    // Добавить расход
    [HttpPost("category/{categoryId}/user/{userId}/spend")]
    public async Task<ActionResult<object>> AddSpending(long userId, int categoryId, [FromQuery] decimal amount)
    {
        var (limit, warningLevel) = await limitService.AddSpendingAsync(userId, categoryId, amount);
        return Ok(new { Limit = limit != null ? ToDto(limit) : null, WarningLevel = warningLevel });
    }

    // Сбросить
    [HttpPost("user/{userId}/reset")]
    public async Task<ActionResult> ResetMonthly(long userId)
    {
        await limitService.ResetMonthlyLimitsAsync(userId);
        return Ok(new { Message = "Лимиты сброшены" });
    }

    // Разблокировать
    [HttpPost("user/{userId}/unblock-expired")]
    public async Task<ActionResult> UnblockExpired(long userId)
    {
        await limitService.UnblockExpiredCategoriesAsync(userId);
        return Ok(new { Message = "Разблокировано" });
    }

    // Удалить
    [HttpDelete("{limitId}/user/{userId}")]
    public async Task<ActionResult> Delete(long userId, int limitId)
    {
        var result = await limitService.DeleteAsync(userId, limitId);
        return result ? Ok(new { Message = "Удалено" }) : NotFound();
    }
}
