using Microsoft.AspNetCore.Mvc;
using Infrastructure.Services;
using Domain.DTOs;

namespace WebApi.Controllers;

// Контроллер пользователей
[ApiController]
[Route("api/users")]
public class UsersController(IUserService userService) : ControllerBase
{
    // Получить пользователя по Telegram ID
    [HttpGet("{telegramId}")]
    public async Task<ActionResult<UserDto>> GetById(long telegramId)
    {
        var user = await userService.GetByIdAsync(telegramId);
        if (user == null) return NotFound();
        
        return Ok(new UserDto(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Username,
            user.Timezone,
            user.IsBalanceHidden,
            user.CreatedAt
        ));
    }
}
