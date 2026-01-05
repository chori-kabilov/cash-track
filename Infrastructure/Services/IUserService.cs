using Domain.Entities;

namespace Infrastructure.Services;

public interface IUserService
{
    Task<User?> GetByIdAsync(long telegramId, CancellationToken cancellationToken = default);
    Task<User> CreateOrUpdateAsync(User user, CancellationToken cancellationToken = default);
}
