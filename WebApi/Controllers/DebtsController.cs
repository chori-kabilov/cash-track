using Microsoft.AspNetCore.Mvc;
using Infrastructure.Services;
using Infrastructure.Mappers;
using Domain.DTOs;
using Domain.Enums;
using Swashbuckle.AspNetCore.Annotations;

namespace WebApi.Controllers;

// Контроллер долгов
[ApiController]
[Route("api/debts")]
[SwaggerTag("Управление долгами")]
public class DebtsController(IDebtService debtService) : ControllerBase
{
    // === READ ===

    [HttpGet("user/{userId}")]
    [SwaggerOperation(Summary = "Все долги")]
    public async Task<ActionResult<IEnumerable<DebtDto>>> GetByUser(long userId)
    {
        var debts = await debtService.GetUserDebtsAsync(userId);
        return Ok(debts.Select(DebtMapper.ToDto));
    }

    [HttpGet("user/{userId}/unpaid")]
    [SwaggerOperation(Summary = "Неоплаченные")]
    public async Task<ActionResult<IEnumerable<DebtDto>>> GetUnpaid(long userId)
    {
        var debts = await debtService.GetUnpaidDebtsAsync(userId);
        return Ok(debts.Select(DebtMapper.ToDto));
    }

    [HttpGet("user/{userId}/paid")]
    [SwaggerOperation(Summary = "Оплаченные", Description = "История погашенных долгов")]
    public async Task<ActionResult<IEnumerable<DebtDto>>> GetPaid(long userId)
    {
        var debts = await debtService.GetPaidDebtsAsync(userId);
        return Ok(debts.Select(DebtMapper.ToDto));
    }

    [HttpGet("user/{userId}/overdue")]
    [SwaggerOperation(Summary = "Просроченные")]
    public async Task<ActionResult<IEnumerable<DebtDto>>> GetOverdue(long userId)
    {
        var debts = await debtService.GetOverdueDebtsAsync(userId);
        return Ok(debts.Select(DebtMapper.ToDto));
    }

    [HttpGet("user/{userId}/type/{type}")]
    [SwaggerOperation(Summary = "По типу", Description = "IOwe или TheyOwe")]
    public async Task<ActionResult<IEnumerable<DebtDto>>> GetByType(long userId, DebtType type)
    {
        var debts = await debtService.GetByTypeAsync(userId, type);
        return Ok(debts.Select(DebtMapper.ToDto));
    }

    [HttpGet("user/{userId}/summary")]
    [SwaggerOperation(Summary = "Сводка", Description = "Общие суммы и количество")]
    public async Task<ActionResult<DebtSummaryDto>> GetSummary(long userId)
    {
        var debts = await debtService.GetUnpaidDebtsAsync(userId);
        var now = DateTimeOffset.UtcNow;
        
        return Ok(new DebtSummaryDto(
            debts.Where(d => d.Type == DebtType.IOwe).Sum(d => d.RemainingAmount),
            debts.Where(d => d.Type == DebtType.TheyOwe).Sum(d => d.RemainingAmount),
            debts.Count(d => d.Type == DebtType.IOwe),
            debts.Count(d => d.Type == DebtType.TheyOwe),
            debts.Count(d => d.DueDate.HasValue && d.DueDate < now)
        ));
    }

    [HttpGet("{debtId}/user/{userId}")]
    [SwaggerOperation(Summary = "По ID")]
    public async Task<ActionResult<DebtDto>> GetById(long userId, int debtId)
    {
        var d = await debtService.GetByIdAsync(userId, debtId);
        return d != null ? Ok(DebtMapper.ToDto(d)) : NotFound();
    }

    // === CREATE ===

    [HttpPost("user/{userId}")]
    [SwaggerOperation(Summary = "Создать долг")]
    public async Task<ActionResult<DebtDto>> Create(
        long userId,
        [FromQuery] string personName,
        [FromQuery] decimal amount,
        [FromQuery] DebtType type,
        [FromQuery] string? description = null,
        [FromQuery] DateTimeOffset? dueDate = null)
    {
        var d = await debtService.CreateAsync(userId, personName, amount, type, description, dueDate);
        return Ok(DebtMapper.ToDto(d));
    }

    // === UPDATE ===

    [HttpPut("{debtId}/user/{userId}")]
    [SwaggerOperation(Summary = "Обновить", Description = "Имя, описание, срок")]
    public async Task<ActionResult<DebtDto>> Update(
        long userId,
        int debtId,
        [FromQuery] string personName,
        [FromQuery] string? description = null,
        [FromQuery] DateTimeOffset? dueDate = null)
    {
        var d = await debtService.UpdateAsync(userId, debtId, personName, description, dueDate);
        return d != null ? Ok(DebtMapper.ToDto(d)) : NotFound();
    }

    [HttpPost("{debtId}/user/{userId}/payment")]
    [SwaggerOperation(Summary = "Частичный платёж")]
    public async Task<ActionResult<DebtDto>> MakePayment(long userId, int debtId, [FromQuery] decimal amount)
    {
        if (amount <= 0) return BadRequest(new { Error = "Сумма должна быть > 0" });
        var (debt, _) = await debtService.RecordPaymentAsync(userId, debtId, amount);
        return debt != null ? Ok(DebtMapper.ToDto(debt)) : NotFound();
    }

    [HttpPut("{debtId}/user/{userId}/paid")]
    [SwaggerOperation(Summary = "Отметить оплаченным")]
    public async Task<ActionResult<DebtDto>> MarkAsPaid(long userId, int debtId)
    {
        var d = await debtService.MarkAsPaidAsync(userId, debtId);
        return d != null ? Ok(DebtMapper.ToDto(d)) : NotFound();
    }

    // === DELETE ===

    [HttpDelete("{debtId}/user/{userId}")]
    [SwaggerOperation(Summary = "Удалить", Description = "Soft delete")]
    public async Task<ActionResult> Delete(long userId, int debtId)
    {
        var result = await debtService.DeleteAsync(userId, debtId);
        return result ? Ok(new { Message = "Долг удалён" }) : NotFound();
    }
}
