using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class UserService(DataContext context) : IUserService
{
    // Получить всех (не удалённых)
    public async Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default)
    {
        return await context.Users.AsNoTracking()
            .Where(u => !u.IsDeleted)
            .OrderByDescending(u => u.UpdatedAt)
            .ToListAsync(ct);
    }

    // Получить по ID
    public async Task<User?> GetByIdAsync(long telegramId, CancellationToken ct = default)
    {
        return await context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == telegramId && !u.IsDeleted, ct);
    }

    // Создать или обновить
    public async Task<User> CreateOrUpdateAsync(User user, CancellationToken ct = default)
    {
        var existing = await context.Users.FirstOrDefaultAsync(u => u.Id == user.Id, ct);
        
        if (existing == null)
        {
            user.CreatedAt = DateTimeOffset.UtcNow;
            user.UpdatedAt = user.CreatedAt;
            context.Users.Add(user);
        }
        else
        {
            existing.FirstName = user.FirstName;
            existing.LastName = user.LastName;
            existing.Username = user.Username;
            existing.LanguageCode = user.LanguageCode;
            existing.LastMessageAt = DateTimeOffset.UtcNow;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await context.SaveChangesAsync(ct);
        return existing ?? user;
    }

    // Обновить настройки
    public async Task<User?> UpdateSettingsAsync(long telegramId, string timezone, bool isBalanceHidden, CancellationToken ct = default)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == telegramId && !u.IsDeleted, ct);
        if (user == null) return null;

        user.Timezone = timezone;
        user.IsBalanceHidden = isBalanceHidden;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(ct);
        return user;
    }

    // Soft Delete
    public async Task<bool> DeleteAsync(long telegramId, CancellationToken ct = default)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == telegramId && !u.IsDeleted, ct);
        if (user == null) return false;

        user.IsDeleted = true;
        user.DeletedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(ct);
        return true;
    }

    // Существует ли
    public async Task<bool> ExistsAsync(long telegramId, CancellationToken ct = default)
    {
        return await context.Users.AnyAsync(u => u.Id == telegramId && !u.IsDeleted, ct);
    }

    // Количество пользователей
    public async Task<int> CountAsync(CancellationToken ct = default)
    {
        return await context.Users.CountAsync(u => !u.IsDeleted, ct);
    }
}
