using Domain.Entities;

namespace Infrastructure.Services;

public interface IAccountService
{
    // CRUD
    Task<IReadOnlyList<Account>> GetAllAsync(CancellationToken ct = default);
    Task<Account?> GetByIdAsync(int accountId, CancellationToken ct = default);
    Task<Account?> GetUserAccountAsync(long userId, CancellationToken ct = default);
    Task<Account> CreateAccountAsync(long userId, string name = "Основной счёт", CancellationToken ct = default);
    Task<Account?> UpdateAsync(int accountId, string name, string currency, CancellationToken ct = default);
    Task<bool> DeleteAsync(int accountId, CancellationToken ct = default);
    
    // Операции с балансом
    Task<Account?> DepositAsync(int accountId, decimal amount, CancellationToken ct = default);
    Task<Account?> WithdrawAsync(int accountId, decimal amount, CancellationToken ct = default);
    Task<Account?> SetBalanceAsync(int accountId, decimal balance, CancellationToken ct = default);
    Task UpdateBalanceAsync(int accountId, decimal amount, CancellationToken ct = default);
    
    // Проверки
    Task<bool> ExistsAsync(long userId, CancellationToken ct = default);
}
