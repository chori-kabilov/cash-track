using Domain.Entities;

namespace Infrastructure.Services;

public interface IAccountService
{
    Task<Account?> GetUserAccountAsync(long userId, CancellationToken cancellationToken = default);
    Task<Account> CreateAccountAsync(long userId, string name = "Основной счёт", CancellationToken cancellationToken = default);
    Task UpdateBalanceAsync(int accountId, decimal amount, CancellationToken cancellationToken = default);
}
