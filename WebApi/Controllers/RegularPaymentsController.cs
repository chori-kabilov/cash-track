using Microsoft.AspNetCore.Mvc;
using Infrastructure.Services;
using Infrastructure.Mappers;
using Domain.DTOs;
using Domain.Enums;
using Swashbuckle.AspNetCore.Annotations;

namespace WebApi.Controllers;

// Контроллер регулярных платежей
[ApiController]
[Route("api/regular-payments")]
[SwaggerTag("Управление регулярными платежами")]
public class RegularPaymentsController(IRegularPaymentService regularPaymentService) : ControllerBase
{
    // === READ ===

    [HttpGet("user/{userId}")]
    [SwaggerOperation(Summary = "Все платежи")]
    public async Task<ActionResult<IEnumerable<RegularPaymentDto>>> GetByUser(long userId)
    {
        var payments = await regularPaymentService.GetUserPaymentsAsync(userId);
        return Ok(payments.Select(RegularPaymentMapper.ToDto));
    }

    [HttpGet("user/{userId}/active")]
    [SwaggerOperation(Summary = "Активные", Description = "Не на паузе")]
    public async Task<ActionResult<IEnumerable<RegularPaymentDto>>> GetActive(long userId)
    {
        var payments = await regularPaymentService.GetActiveAsync(userId);
        return Ok(payments.Select(RegularPaymentMapper.ToDto));
    }

    [HttpGet("user/{userId}/frequency/{frequency}")]
    [SwaggerOperation(Summary = "По частоте", Description = "Daily/Weekly/Monthly/Yearly")]
    public async Task<ActionResult<IEnumerable<RegularPaymentDto>>> GetByFrequency(long userId, PaymentFrequency frequency)
    {
        var payments = await regularPaymentService.GetByFrequencyAsync(userId, frequency);
        return Ok(payments.Select(RegularPaymentMapper.ToDto));
    }

    [HttpGet("user/{userId}/due")]
    [SwaggerOperation(Summary = "Предстоящие", Description = "Скоро нужно оплатить")]
    public async Task<ActionResult<IEnumerable<RegularPaymentDto>>> GetDue(long userId)
    {
        var payments = await regularPaymentService.GetDuePaymentsAsync(userId);
        return Ok(payments.Select(RegularPaymentMapper.ToDto));
    }

    [HttpGet("user/{userId}/overdue")]
    [SwaggerOperation(Summary = "Просроченные")]
    public async Task<ActionResult<IEnumerable<RegularPaymentDto>>> GetOverdue(long userId)
    {
        var payments = await regularPaymentService.GetOverduePaymentsAsync(userId);
        return Ok(payments.Select(RegularPaymentMapper.ToDto));
    }

    [HttpGet("user/{userId}/summary")]
    [SwaggerOperation(Summary = "Сводка")]
    public async Task<ActionResult<RegularPaymentSummaryDto>> GetSummary(long userId)
    {
        var payments = await regularPaymentService.GetUserPaymentsAsync(userId);
        var now = DateTimeOffset.UtcNow;
        
        return Ok(new RegularPaymentSummaryDto(
            payments.Count,
            payments.Count(p => !p.IsPaused),
            payments.Count(p => p.IsPaused),
            payments.Where(p => !p.IsPaused && p.Frequency == PaymentFrequency.Monthly).Sum(p => p.Amount),
            payments.Count(p => !p.IsPaused && p.NextDueDate <= now)
        ));
    }

    [HttpGet("{paymentId}/user/{userId}")]
    [SwaggerOperation(Summary = "По ID")]
    public async Task<ActionResult<RegularPaymentDto>> GetById(long userId, int paymentId)
    {
        var r = await regularPaymentService.GetByIdAsync(userId, paymentId);
        return r != null ? Ok(RegularPaymentMapper.ToDto(r)) : NotFound();
    }

    // === CREATE ===

    [HttpPost("user/{userId}")]
    [SwaggerOperation(Summary = "Создать платёж")]
    public async Task<ActionResult<RegularPaymentDto>> Create(
        long userId,
        [FromQuery] string name,
        [FromQuery] decimal amount,
        [FromQuery] PaymentFrequency frequency,
        [FromQuery] int? categoryId = null,
        [FromQuery] int? dayOfMonth = null,
        [FromQuery] int reminderDaysBefore = 3)
    {
        var r = await regularPaymentService.CreateAsync(userId, name, amount, frequency, categoryId, dayOfMonth, reminderDaysBefore);
        return Ok(RegularPaymentMapper.ToDto(r));
    }

    // === UPDATE ===

    [HttpPut("{paymentId}/user/{userId}")]
    [SwaggerOperation(Summary = "Обновить", Description = "Имя, сумма, категория")]
    public async Task<ActionResult<RegularPaymentDto>> Update(
        long userId,
        int paymentId,
        [FromQuery] string name,
        [FromQuery] decimal amount,
        [FromQuery] int? categoryId = null)
    {
        var r = await regularPaymentService.UpdateAsync(userId, paymentId, name, amount, categoryId);
        return r != null ? Ok(RegularPaymentMapper.ToDto(r)) : NotFound();
    }

    [HttpPost("{paymentId}/user/{userId}/paid")]
    [SwaggerOperation(Summary = "Оплатить", Description = "Пересчитывает следующую дату")]
    public async Task<ActionResult<RegularPaymentDto>> MarkAsPaid(long userId, int paymentId)
    {
        var r = await regularPaymentService.MarkAsPaidAsync(userId, paymentId);
        return r != null ? Ok(RegularPaymentMapper.ToDto(r)) : NotFound();
    }

    [HttpPut("{paymentId}/user/{userId}/pause")]
    [SwaggerOperation(Summary = "Пауза/возобновить")]
    public async Task<ActionResult<RegularPaymentDto>> SetPaused(long userId, int paymentId, [FromQuery] bool isPaused)
    {
        var r = await regularPaymentService.SetPausedAsync(userId, paymentId, isPaused);
        return r != null ? Ok(RegularPaymentMapper.ToDto(r)) : NotFound();
    }

    // === DELETE ===

    [HttpDelete("{paymentId}/user/{userId}")]
    [SwaggerOperation(Summary = "Удалить", Description = "Soft delete")]
    public async Task<ActionResult> Delete(long userId, int paymentId)
    {
        var result = await regularPaymentService.DeleteAsync(userId, paymentId);
        return result ? Ok(new { Message = "Платёж удалён" }) : NotFound();
    }
}
