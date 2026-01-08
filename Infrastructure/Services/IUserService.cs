using Domain.Entities;

namespace Infrastructure.Services;

public interface IUserService
{
    // CRUD
    Task<IReadOnlyList<User>> GetAllAsync(CancellationToken ct = default);
    Task<User?> GetByIdAsync(long telegramId, CancellationToken ct = default);
    Task<User> CreateOrUpdateAsync(User user, CancellationToken ct = default);
    Task<User?> UpdateSettingsAsync(long telegramId, string timezone, bool isBalanceHidden, CancellationToken ct = default);
    Task<bool> DeleteAsync(long telegramId, CancellationToken ct = default);
    
    // Проверки
    Task<bool> ExistsAsync(long telegramId, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default);
}
