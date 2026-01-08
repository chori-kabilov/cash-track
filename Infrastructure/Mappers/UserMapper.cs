using Domain.DTOs;
using Domain.Entities;

namespace Infrastructure.Mappers;

// Маппер для User
public static class UserMapper
{
    public static UserDto ToDto(User user)
    {
        return new UserDto(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Username,
            user.Timezone,
            user.IsBalanceHidden,
            user.CreatedAt
        );
    }
}
