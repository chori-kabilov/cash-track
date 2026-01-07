using Microsoft.AspNetCore.Mvc;
using Infrastructure.Services;
using Domain.DTOs;
using Domain.Enums;

namespace WebApi.Controllers;

// Контроллер регулярных платежей
[ApiController]
[Route("api/regular-payments")]
public class RegularPaymentsController(IRegularPaymentService regularPaymentService) : ControllerBase
{
    // Маппинг в DTO
    private static RegularPaymentDto ToDto(Domain.Entities.RegularPayment r) => new(
        r.Id,
        r.Name,
        r.Amount,
        r.Frequency,
        r.Frequency switch { PaymentFrequency.Daily => "Ежедневно", PaymentFrequency.Weekly => "Еженедельно", PaymentFrequency.Monthly => "Ежемесячно", _ => "Ежегодно" },
        r.DayOfMonth,
        r.NextDueDate,
        r.LastPaidDate,
        r.IsPaused,
        r.NextDueDate < DateTimeOffset.UtcNow && !r.IsPaused,
        r.Category != null ? new CategoryDto(r.Category.Id, r.Category.Name, r.Category.Icon, r.Category.Type, r.Category.Priority, r.Category.IsActive) : null
    );

    // Получить все
    [HttpGet("user/{userId}")]
    public async Task<ActionResult<IEnumerable<RegularPaymentDto>>> GetByUser(long userId)
    {
        var payments = await regularPaymentService.GetUserPaymentsAsync(userId);
        return Ok(payments.Select(ToDto));
    }

    // Предстоящие
    [HttpGet("user/{userId}/due")]
    public async Task<ActionResult<IEnumerable<RegularPaymentDto>>> GetDue(long userId)
    {
        var payments = await regularPaymentService.GetDuePaymentsAsync(userId);
        return Ok(payments.Select(ToDto));
    }

    // Просроченные
    [HttpGet("user/{userId}/overdue")]
    public async Task<ActionResult<IEnumerable<RegularPaymentDto>>> GetOverdue(long userId)
    {
        var payments = await regularPaymentService.GetOverduePaymentsAsync(userId);
        return Ok(payments.Select(ToDto));
    }

    // Сводка
    [HttpGet("user/{userId}/summary")]
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

    // По ID
    [HttpGet("{paymentId}/user/{userId}")]
    public async Task<ActionResult<RegularPaymentDto>> GetById(long userId, int paymentId)
    {
        var r = await regularPaymentService.GetByIdAsync(userId, paymentId);
        return r != null ? Ok(ToDto(r)) : NotFound();
    }

    // Создать
    [HttpPost("user/{userId}")]
    public async Task<ActionResult<RegularPaymentDto>> Create(long userId, [FromQuery] string name, [FromQuery] decimal amount, [FromQuery] PaymentFrequency frequency, [FromQuery] int? categoryId = null, [FromQuery] int? dayOfMonth = null, [FromQuery] int reminderDaysBefore = 3)
    {
        var r = await regularPaymentService.CreateAsync(userId, name, amount, frequency, categoryId, dayOfMonth, reminderDaysBefore);
        return Ok(ToDto(r));
    }

    // Оплатить
    [HttpPost("{paymentId}/user/{userId}/paid")]
    public async Task<ActionResult<RegularPaymentDto>> MarkAsPaid(long userId, int paymentId)
    {
        var r = await regularPaymentService.MarkAsPaidAsync(userId, paymentId);
        return r != null ? Ok(ToDto(r)) : NotFound();
    }

    // Пауза
    [HttpPut("{paymentId}/user/{userId}/pause")]
    public async Task<ActionResult<RegularPaymentDto>> SetPaused(long userId, int paymentId, [FromQuery] bool isPaused)
    {
        var r = await regularPaymentService.SetPausedAsync(userId, paymentId, isPaused);
        return r != null ? Ok(ToDto(r)) : NotFound();
    }

    // Удалить
    [HttpDelete("{paymentId}/user/{userId}")]
    public async Task<ActionResult> Delete(long userId, int paymentId)
    {
        var result = await regularPaymentService.DeleteAsync(userId, paymentId);
        return result ? Ok(new { Message = "Удалено" }) : NotFound();
    }
}
