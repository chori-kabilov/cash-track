using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class TransactionService(DataContext context) : ITransactionService
{
    public async Task<Transaction> CreateTransactionAsync(
        int accountId, 
        int categoryId, 
        decimal amount, 
        TransactionType type, 
        string? description = null, 
        bool isImpulsive = false,
        bool isDraft = false,
        bool isError = false,
        DateTimeOffset? date = null,
        CancellationToken cancellationToken = default)
    {
        var transaction = new Transaction
        {
            AccountId = accountId,
            CategoryId = categoryId,
            Amount = amount,
            Type = type,
            Description = description,
            IsImpulsive = isImpulsive,
            IsDraft = isDraft,
            IsError = isError,
            Date = date ?? DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        };

        context.Transactions.Add(transaction);
        await context.SaveChangesAsync(cancellationToken);
        return transaction;
    }

    public async Task<IReadOnlyList<Transaction>> GetUserTransactionsAsync(long userId, int limit = 10, CancellationToken cancellationToken = default)
    {
        return await context.Transactions
            .AsNoTracking()
            .Include(t => t.Category)
            .Where(t => t.Account.UserId == userId && !t.IsDeleted)
            .OrderByDescending(t => t.Date)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<Transaction> Items, int TotalCount)> GetTransactionsPageAsync(long userId, int page, int pageSize, TransactionType? type = null, DateTimeOffset? fromDateUtc = null, string? search = null, CancellationToken cancellationToken = default)
    {
        var query = context.Transactions
            .AsNoTracking()
            .Include(t => t.Category)
            .Where(t => t.Account.UserId == userId && !t.IsDeleted);

        if (type.HasValue)
        {
            query = query.Where(t => t.Type == type.Value);
        }

        if (fromDateUtc.HasValue)
        {
            var utc = fromDateUtc.Value.ToUniversalTime();
            query = query.Where(t => t.Date >= utc);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(t =>
                (t.Description != null && t.Description.ToLower().Contains(term)) ||
                (t.Category != null && t.Category.Name.ToLower().Contains(term)));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(t => t.Date)
            .Skip((Math.Max(page, 1) - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, total);
    }

    public async Task<Transaction?> GetLastTransactionAsync(long userId, CancellationToken cancellationToken = default)
    {
        return await context.Transactions
            .AsNoTracking()
            .Include(t => t.Category)
            .Where(t => t.Account.UserId == userId && !t.IsDeleted)
            .OrderByDescending(t => t.Date)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<decimal> GetTotalIncomeAsync(long userId, DateTimeOffset? fromDateUtc = null, CancellationToken cancellationToken = default)
    {
        var query = context.Transactions
            .AsNoTracking()
            .Where(t => t.Account.UserId == userId && t.Type == TransactionType.Income && !t.IsDeleted);

        if (fromDateUtc.HasValue)
        {
            var utc = fromDateUtc.Value.ToUniversalTime();
            query = query.Where(t => t.Date >= utc);
        }

        return await query.SumAsync(t => t.Amount, cancellationToken);
    }

    public async Task<decimal> GetTotalExpenseAsync(long userId, DateTimeOffset? fromDateUtc = null, CancellationToken cancellationToken = default)
    {
        var query = context.Transactions
            .AsNoTracking()
            .Where(t => t.Account.UserId == userId && t.Type == TransactionType.Expense && !t.IsDeleted);

        if (fromDateUtc.HasValue)
        {
            var utc = fromDateUtc.Value.ToUniversalTime();
            query = query.Where(t => t.Date >= utc);
        }

        return await query.SumAsync(t => t.Amount, cancellationToken);
    }

    public async Task<decimal> GetCategoryExpenseAsync(long userId, int categoryId, DateTimeOffset fromDateUtc, CancellationToken cancellationToken = default)
    {
        var utc = fromDateUtc.ToUniversalTime();
        return await context.Transactions
            .AsNoTracking()
            .Where(t => t.Account.UserId == userId && t.CategoryId == categoryId && t.Type == TransactionType.Expense && !t.IsDeleted && t.Date >= utc)
            .SumAsync(t => t.Amount, cancellationToken);
    }

    public async Task<IReadOnlyList<int>> GetRecentCategoryIdsAsync(long userId, TransactionType type, int limit = 6, CancellationToken cancellationToken = default)
    {
        return await context.Transactions
            .AsNoTracking()
            .Where(t => t.Account.UserId == userId && t.Type == type && !t.IsDeleted)
            .GroupBy(t => t.CategoryId)
            .Select(g => new { CategoryId = g.Key, LastDate = g.Max(x => x.Date) })
            .OrderByDescending(x => x.LastDate)
            .Take(limit)
            .Select(x => x.CategoryId)
            .ToListAsync(cancellationToken);
    }
    public async Task<IReadOnlyList<(Category Category, decimal Amount)>> GetTopExpensesAsync(long userId, DateTimeOffset fromDate, int count, CancellationToken cancellationToken = default)
    {
        var grouped = await context.Transactions
            .AsNoTracking()
            .Where(t => t.Account.UserId == userId && t.Date >= fromDate && t.Type == TransactionType.Expense && !t.IsDeleted)
            .GroupBy(t => t.CategoryId)
            .Select(g => new { CategoryId = g.Key, Total = g.Sum(t => t.Amount) })
            .OrderByDescending(x => x.Total)
            .Take(count)
            .ToListAsync(cancellationToken);

        var result = new List<(Category Category, decimal Amount)>();
        foreach (var item in grouped)
        {
            var category = await context.Categories.FindAsync(new object[] { item.CategoryId }, cancellationToken);
            if (category != null)
            {
                result.Add((category, item.Total));
            }
        }
        return result;
    }

    public async Task<Transaction> ProcessTransactionAsync(
        long userId,
        int categoryId,
        decimal amount,
        TransactionType type,
        string? description = null,
        bool isImpulsive = false,
        DateTimeOffset? date = null,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0) throw new ArgumentException("Amount must be positive");

        using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var account = await context.Accounts
                .FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);

            if (account == null)
            {
                account = new Account
                {
                    UserId = userId,
                    Name = "Основной счёт",
                    Balance = 0,
                    Currency = "TJS",
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                context.Accounts.Add(account);
                await context.SaveChangesAsync(cancellationToken);
            }

            if (type == TransactionType.Expense)
            {
                if (account.Balance < amount)
                {
                   throw new InvalidOperationException($"Недостаточно средств. Баланс: {account.Balance}, Требуется: {amount}");
                }
                account.Balance -= amount;
            }
            else
            {
                account.Balance += amount;
            }
            account.UpdatedAt = DateTimeOffset.UtcNow;

            var txn = new Transaction
            {
                AccountId = account.Id,
                CategoryId = categoryId,
                Amount = amount,
                Type = type,
                Description = description,
                IsImpulsive = isImpulsive,
                IsDraft = false,
                IsError = false,
                Date = date ?? DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow
            };

            context.Transactions.Add(txn);
            await context.SaveChangesAsync(cancellationToken);
            
            await transaction.CommitAsync(cancellationToken);
            return txn;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    // Получить по ID
    public async Task<Transaction?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await context.Transactions.AsNoTracking()
            .Include(t => t.Category)
            .FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted, ct);
    }

    // Получить по категории
    public async Task<IReadOnlyList<Transaction>> GetByCategoryAsync(long userId, int categoryId, int limit = 50, CancellationToken ct = default)
    {
        return await context.Transactions.AsNoTracking()
            .Include(t => t.Category)
            .Where(t => t.Account.UserId == userId && t.CategoryId == categoryId && !t.IsDeleted)
            .OrderByDescending(t => t.Date)
            .Take(limit)
            .ToListAsync(ct);
    }

    // Обновить описание
    public async Task<Transaction?> UpdateDescriptionAsync(int id, string? description, CancellationToken ct = default)
    {
        var txn = await context.Transactions.FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted, ct);
        if (txn == null) return null;

        txn.Description = description;
        await context.SaveChangesAsync(ct);
        
        return await context.Transactions.AsNoTracking()
            .Include(t => t.Category)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    // Отменить (пометить как ошибочную)
    public async Task<Transaction?> CancelAsync(int id, CancellationToken ct = default)
    {
        var txn = await context.Transactions.FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted && !t.IsError, ct);
        if (txn == null) return null;

        // Вернуть деньги на счёт
        var account = await context.Accounts.FirstOrDefaultAsync(a => a.Id == txn.AccountId, ct);
        if (account != null)
        {
            if (txn.Type == TransactionType.Expense)
                account.Balance += txn.Amount;
            else
                account.Balance -= txn.Amount;
            account.UpdatedAt = DateTimeOffset.UtcNow;
        }

        txn.IsError = true;
        await context.SaveChangesAsync(ct);
        
        return await context.Transactions.AsNoTracking()
            .Include(t => t.Category)
            .FirstOrDefaultAsync(t => t.Id == id, ct);
    }

    // Soft Delete
    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var txn = await context.Transactions.FirstOrDefaultAsync(t => t.Id == id && !t.IsDeleted, ct);
        if (txn == null) return false;

        txn.IsDeleted = true;
        txn.DeletedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(ct);
        return true;
    }
}
