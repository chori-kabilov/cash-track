using Microsoft.AspNetCore.Mvc;
using Infrastructure.Services;
using Domain.DTOs;
using Domain.Enums;

namespace WebApi.Controllers;

// Контроллер категорий
[ApiController]
[Route("api/categories")]
public class CategoriesController(ICategoryService categoryService) : ControllerBase
{
    // Получить все категории
    [HttpGet("user/{userId}")]
    public async Task<ActionResult<IEnumerable<CategoryDto>>> GetByUser(long userId)
    {
        var categories = await categoryService.GetUserCategoriesAsync(userId);
        var dtos = categories.Select(c => new CategoryDto(c.Id, c.Name, c.Icon, c.Type, c.Priority, c.IsActive));
        return Ok(dtos);
    }

    // Получить категорию по ID
    [HttpGet("{categoryId}/user/{userId}")]
    public async Task<ActionResult<CategoryDto>> GetById(long userId, int categoryId)
    {
        var c = await categoryService.GetCategoryByIdAsync(userId, categoryId);
        if (c == null) return NotFound();
        return Ok(new CategoryDto(c.Id, c.Name, c.Icon, c.Type, c.Priority, c.IsActive));
    }

    // Найти по имени
    [HttpGet("user/{userId}/search")]
    public async Task<ActionResult<CategoryDto>> GetByName(long userId, [FromQuery] string name)
    {
        var c = await categoryService.GetByNameAsync(userId, name);
        if (c == null) return NotFound();
        return Ok(new CategoryDto(c.Id, c.Name, c.Icon, c.Type, c.Priority, c.IsActive));
    }

    // Создать категорию
    [HttpPost("user/{userId}")]
    public async Task<ActionResult<CategoryDto>> Create(
        long userId,
        [FromQuery] string name,
        [FromQuery] TransactionType type,
        [FromQuery] string icon = "📁")
    {
        var c = await categoryService.CreateAsync(userId, name, type, icon);
        return Ok(new CategoryDto(c.Id, c.Name, c.Icon, c.Type, c.Priority, c.IsActive));
    }

    // Инициализировать стандартные
    [HttpPost("user/{userId}/init")]
    public async Task<ActionResult> InitDefaults(long userId)
    {
        await categoryService.InitializeDefaultCategoriesAsync(userId);
        return Ok(new { Message = "Категории созданы" });
    }

    // Переименовать
    [HttpPut("{categoryId}/user/{userId}/rename")]
    public async Task<ActionResult<CategoryDto>> Rename(long userId, int categoryId, [FromQuery] string newName)
    {
        var c = await categoryService.RenameAsync(userId, categoryId, newName);
        if (c == null) return NotFound();
        return Ok(new CategoryDto(c.Id, c.Name, c.Icon, c.Type, c.Priority, c.IsActive));
    }

    // Удалить
    [HttpDelete("{categoryId}/user/{userId}")]
    public async Task<ActionResult> Delete(long userId, int categoryId)
    {
        var result = await categoryService.DeleteAsync(userId, categoryId);
        return result ? Ok(new { Message = "Удалено" }) : NotFound();
    }
}
