using Microsoft.AspNetCore.Mvc;
using Infrastructure.Services;
using Infrastructure.Mappers;
using Domain.DTOs;
using Swashbuckle.AspNetCore.Annotations;

namespace WebApi.Controllers;

// Контроллер пользователей
[ApiController]
[Route("api/users")]
[SwaggerTag("Управление пользователями")]
public class UsersController(IUserService userService) : ControllerBase
{
    // === READ ===
    
    [HttpGet]
    [SwaggerOperation(Summary = "Получить всех пользователей", Description = "Список всех активных пользователей")]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetAll()
    {
        var users = await userService.GetAllAsync();
        return Ok(users.Select(UserMapper.ToDto));
    }

    [HttpGet("{telegramId}")]
    [SwaggerOperation(Summary = "Получить пользователя", Description = "По Telegram ID")]
    public async Task<ActionResult<UserDto>> GetById(long telegramId)
    {
        var user = await userService.GetByIdAsync(telegramId);
        return user != null ? Ok(UserMapper.ToDto(user)) : NotFound();
    }

    [HttpGet("{telegramId}/exists")]
    [SwaggerOperation(Summary = "Проверить наличие", Description = "Существует ли пользователь")]
    public async Task<ActionResult<object>> Exists(long telegramId)
    {
        var exists = await userService.ExistsAsync(telegramId);
        return Ok(new { TelegramId = telegramId, Exists = exists });
    }

    [HttpGet("count")]
    [SwaggerOperation(Summary = "Количество пользователей", Description = "Всего активных пользователей")]
    public async Task<ActionResult<object>> Count()
    {
        var count = await userService.CountAsync();
        return Ok(new { Count = count });
    }

    // === UPDATE ===

    [HttpPut("{telegramId}/settings")]
    [SwaggerOperation(Summary = "Обновить настройки", Description = "Часовой пояс и скрытие баланса")]
    public async Task<ActionResult<UserDto>> UpdateSettings(
        long telegramId, 
        [FromQuery] string timezone = "Asia/Dushanbe", 
        [FromQuery] bool isBalanceHidden = true)
    {
        var user = await userService.UpdateSettingsAsync(telegramId, timezone, isBalanceHidden);
        return user != null ? Ok(UserMapper.ToDto(user)) : NotFound();
    }

    // === DELETE ===

    [HttpDelete("{telegramId}")]
    [SwaggerOperation(Summary = "Удалить пользователя", Description = "Мягкое удаление (soft-delete)")]
    public async Task<ActionResult> Delete(long telegramId)
    {
        var result = await userService.DeleteAsync(telegramId);
        return result ? Ok(new { Message = "Пользователь удалён" }) : NotFound();
    }
}
