using Domain.DTOs;
using Domain.Entities;
using Infrastructure.Mappers;

namespace Infrastructure.Mappers;

// Маппер для Limit
public static class LimitMapper
{
    public static LimitDto ToDto(Limit l)
    {
        var percent = l.Amount > 0 ? Math.Round((double)(l.SpentAmount / l.Amount) * 100, 1) : 0;
        var status = percent >= 100 ? "Превышен" 
            : percent >= 80 ? "Предупреждение" 
            : percent >= 50 ? "Внимание" 
            : "OK";

        return new LimitDto(
            l.Id,
            l.Amount,
            l.SpentAmount,
            l.Amount - l.SpentAmount,
            percent,
            status,
            l.IsBlocked,
            l.Category != null ? CategoryMapper.ToDto(l.Category) : null
        );
    }
}
