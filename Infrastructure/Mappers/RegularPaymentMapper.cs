using Domain.DTOs;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Mappers;

namespace Infrastructure.Mappers;

// Маппер для RegularPayment
public static class RegularPaymentMapper
{
    public static RegularPaymentDto ToDto(RegularPayment r)
    {
        var frequencyDescription = r.Frequency switch
        {
            PaymentFrequency.Daily => "Ежедневно",
            PaymentFrequency.Weekly => "Еженедельно",
            PaymentFrequency.Monthly => "Ежемесячно",
            _ => "Ежегодно"
        };
        
        var isOverdue = r.NextDueDate.HasValue && r.NextDueDate < DateTimeOffset.UtcNow && !r.IsPaused;

        return new RegularPaymentDto(
            r.Id,
            r.Name,
            r.Amount,
            r.Frequency,
            frequencyDescription,
            r.DayOfMonth,
            r.NextDueDate,
            r.LastPaidDate,
            r.IsPaused,
            isOverdue,
            r.Category != null ? CategoryMapper.ToDto(r.Category) : null
        );
    }
}
