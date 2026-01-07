using Microsoft.AspNetCore.Mvc;
using Infrastructure.Services;
using Domain.DTOs;
using Domain.Enums;

namespace WebApi.Controllers;

// Контроллер долгов
[ApiController]
[Route("api/debts")]
public class DebtsController(IDebtService debtService) : ControllerBase
{
    // Маппинг в DTO
    private static DebtDto ToDto(Domain.Entities.Debt d) => new(
        d.Id,
        d.PersonName,
        d.Amount,
        d.RemainingAmount,
        d.Type,
        d.Type == DebtType.IOwe ? "Я должен" : "Мне должны",
        d.Description,
        d.TakenDate,
        d.DueDate,
        d.IsPaid,
        d.DueDate.HasValue && d.DueDate < DateTimeOffset.UtcNow && !d.IsPaid
    );

    // Получить все долги
    [HttpGet("user/{userId}")]
    public async Task<ActionResult<IEnumerable<DebtDto>>> GetByUser(long userId)
    {
        var debts = await debtService.GetUserDebtsAsync(userId);
        return Ok(debts.Select(ToDto));
    }

    // Неоплаченные
    [HttpGet("user/{userId}/unpaid")]
    public async Task<ActionResult<IEnumerable<DebtDto>>> GetUnpaid(long userId)
    {
        var debts = await debtService.GetUnpaidDebtsAsync(userId);
        return Ok(debts.Select(ToDto));
    }

    // Просроченные
    [HttpGet("user/{userId}/overdue")]
    public async Task<ActionResult<IEnumerable<DebtDto>>> GetOverdue(long userId)
    {
        var debts = await debtService.GetOverdueDebtsAsync(userId);
        return Ok(debts.Select(ToDto));
    }

    // Сводка
    [HttpGet("user/{userId}/summary")]
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

    // Получить по ID
    [HttpGet("{debtId}/user/{userId}")]
    public async Task<ActionResult<DebtDto>> GetById(long userId, int debtId)
    {
        var d = await debtService.GetByIdAsync(userId, debtId);
        return d != null ? Ok(ToDto(d)) : NotFound();
    }

    // Создать
    [HttpPost("user/{userId}")]
    public async Task<ActionResult<DebtDto>> Create(long userId, [FromQuery] string personName, [FromQuery] decimal amount, [FromQuery] DebtType type, [FromQuery] string? description = null, [FromQuery] DateTimeOffset? dueDate = null)
    {
        var d = await debtService.CreateAsync(userId, personName, amount, type, description, dueDate);
        return Ok(ToDto(d));
    }

    // Платёж
    [HttpPost("{debtId}/user/{userId}/payment")]
    public async Task<ActionResult<DebtDto>> MakePayment(long userId, int debtId, [FromQuery] decimal amount)
    {
        var d = await debtService.MakePaymentAsync(userId, debtId, amount);
        return d != null ? Ok(ToDto(d)) : NotFound();
    }

    // Оплатить
    [HttpPut("{debtId}/user/{userId}/paid")]
    public async Task<ActionResult<DebtDto>> MarkAsPaid(long userId, int debtId)
    {
        var d = await debtService.MarkAsPaidAsync(userId, debtId);
        return d != null ? Ok(ToDto(d)) : NotFound();
    }

    // Удалить
    [HttpDelete("{debtId}/user/{userId}")]
    public async Task<ActionResult> Delete(long userId, int debtId)
    {
        var result = await debtService.DeleteAsync(userId, debtId);
        return result ? Ok(new { Message = "Удалено" }) : NotFound();
    }
}
