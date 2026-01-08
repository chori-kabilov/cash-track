using Domain.DTOs;
using Domain.Entities;

namespace Infrastructure.Mappers;

// Маппер для Account
public static class AccountMapper
{
    public static AccountDto ToDto(Account account)
    {
        return new AccountDto(
            account.Id,
            account.UserId,
            account.Name,
            account.Balance,
            account.Currency,
            account.UpdatedAt
        );
    }
}
