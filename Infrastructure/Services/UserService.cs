using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class UserService(DataContext context) : IUserService
{
    public async Task<User?> GetByIdAsync(long telegramId, CancellationToken cancellationToken = default)
    {
        return await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == telegramId, cancellationToken);
    }

    public async Task<User> CreateOrUpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        var existingUser = await context.Users.FirstOrDefaultAsync(x => x.Id == user.Id, cancellationToken);
        
        if (existingUser == null)
        {
            user.CreatedAt = DateTimeOffset.UtcNow;
            user.UpdatedAt = user.CreatedAt;
            context.Users.Add(user);
        }
        else
        {
            existingUser.FirstName = user.FirstName;
            existingUser.LastName = user.LastName;
            existingUser.Username = user.Username;
            existingUser.LanguageCode = user.LanguageCode;
            existingUser.LastMessageAt = DateTimeOffset.UtcNow;
            existingUser.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);
        return existingUser ?? user;
    }
}
