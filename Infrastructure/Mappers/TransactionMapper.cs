using Domain.DTOs;
using Domain.Entities;

namespace Infrastructure.Mappers;

// Маппер для Transaction
public static class TransactionMapper
{
    public static TransactionDto ToDto(Transaction t)
    {
        return new TransactionDto(
            t.Id,
            t.Amount,
            t.Type,
            t.Description,
            t.IsImpulsive,
            t.Date,
            t.Category != null ? CategoryMapper.ToDto(t.Category) : null
        );
    }
}
