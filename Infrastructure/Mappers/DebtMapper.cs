using Domain.DTOs;
using Domain.Entities;
using Domain.Enums;

namespace Infrastructure.Mappers;

// Маппер для Debt
public static class DebtMapper
{
    public static DebtDto ToDto(Debt d)
    {
        var typeDescription = d.Type == DebtType.IOwe ? "Я должен" : "Мне должны";
        var isOverdue = d.DueDate.HasValue && d.DueDate < DateTimeOffset.UtcNow && !d.IsPaid;

        return new DebtDto(
            d.Id,
            d.PersonName,
            d.Amount,
            d.RemainingAmount,
            d.Type,
            typeDescription,
            d.Description,
            d.TakenDate,
            d.DueDate,
            d.IsPaid,
            isOverdue
        );
    }
}
