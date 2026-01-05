using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class AccountService(DataContext context) : IAccountService
{
    public async Task<Account?> GetUserAccountAsync(long userId, CancellationToken cancellationToken = default)
    {
        return await context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);
    }

    public async Task<Account> CreateAccountAsync(long userId, string name = "Основной счёт", CancellationToken cancellationToken = default)
    {
        var account = new Account
        {
            UserId = userId,
            Name = name,
            Balance = 0,
            Currency = "TJS",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        context.Accounts.Add(account);
        await context.SaveChangesAsync(cancellationToken);
        return account;
    }

    public async Task UpdateBalanceAsync(int accountId, decimal amount, CancellationToken cancellationToken = default)
    {
        var account = await context.Accounts.FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);
        if (account != null)
        {
            account.Balance += amount;
            account.UpdatedAt = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
        }
    }
}
