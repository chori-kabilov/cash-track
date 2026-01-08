using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class AccountService(DataContext context) : IAccountService
{
    // Получить все (только не удалённые)
    public async Task<IReadOnlyList<Account>> GetAllAsync(CancellationToken ct = default)
    {
        return await context.Accounts.AsNoTracking()
            .Where(a => !a.IsDeleted)
            .OrderByDescending(a => a.UpdatedAt)
            .ToListAsync(ct);
    }

    // Получить по ID (только не удалённые)
    public async Task<Account?> GetByIdAsync(int accountId, CancellationToken ct = default)
    {
        return await context.Accounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == accountId && !a.IsDeleted, ct);
    }

    // Получить по userId (только не удалённые)
    public async Task<Account?> GetUserAccountAsync(long userId, CancellationToken ct = default)
    {
        return await context.Accounts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.UserId == userId && !a.IsDeleted, ct);
    }

    // Создать
    public async Task<Account> CreateAccountAsync(long userId, string name = "Основной счёт", CancellationToken ct = default)
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
        await context.SaveChangesAsync(ct);
        return account;
    }

    // Обновить
    public async Task<Account?> UpdateAsync(int accountId, string name, string currency, CancellationToken ct = default)
    {
        var account = await context.Accounts.FirstOrDefaultAsync(a => a.Id == accountId && !a.IsDeleted, ct);
        if (account == null) return null;

        account.Name = name;
        account.Currency = currency;
        account.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(ct);
        return account;
    }

    // Soft Delete
    public async Task<bool> DeleteAsync(int accountId, CancellationToken ct = default)
    {
        var account = await context.Accounts.FirstOrDefaultAsync(a => a.Id == accountId && !a.IsDeleted, ct);
        if (account == null) return false;

        account.IsDeleted = true;
        account.DeletedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(ct);
        return true;
    }

    // Пополнить
    public async Task<Account?> DepositAsync(int accountId, decimal amount, CancellationToken ct = default)
    {
        var account = await context.Accounts.FirstOrDefaultAsync(a => a.Id == accountId && !a.IsDeleted, ct);
        if (account == null) return null;

        account.Balance += amount;
        account.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(ct);
        return account;
    }

    // Списать
    public async Task<Account?> WithdrawAsync(int accountId, decimal amount, CancellationToken ct = default)
    {
        var account = await context.Accounts.FirstOrDefaultAsync(a => a.Id == accountId && !a.IsDeleted, ct);
        if (account == null) return null;

        account.Balance -= amount;
        account.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(ct);
        return account;
    }

    // Установить баланс
    public async Task<Account?> SetBalanceAsync(int accountId, decimal balance, CancellationToken ct = default)
    {
        var account = await context.Accounts.FirstOrDefaultAsync(a => a.Id == accountId && !a.IsDeleted, ct);
        if (account == null) return null;

        account.Balance = balance;
        account.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(ct);
        return account;
    }

    // Обновить баланс (добавить/вычесть)
    public async Task UpdateBalanceAsync(int accountId, decimal amount, CancellationToken ct = default)
    {
        var account = await context.Accounts.FirstOrDefaultAsync(a => a.Id == accountId && !a.IsDeleted, ct);
        if (account != null)
        {
            account.Balance += amount;
            account.UpdatedAt = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync(ct);
        }
    }

    // Проверить наличие (только не удалённые)
    public async Task<bool> ExistsAsync(long userId, CancellationToken ct = default)
    {
        return await context.Accounts.AnyAsync(a => a.UserId == userId && !a.IsDeleted, ct);
    }
}
