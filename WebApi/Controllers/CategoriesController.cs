using Microsoft.AspNetCore.Mvc;
using Infrastructure.Services;
using Infrastructure.Mappers;
using Domain.DTOs;
using Domain.Enums;
using Swashbuckle.AspNetCore.Annotations;

namespace WebApi.Controllers;

// Контроллер категорий
[ApiController]
[Route("api/categories")]
[SwaggerTag("Управление категориями")]
public class CategoriesController(ICategoryService categoryService) : ControllerBase
{
    // === READ ===

    [HttpGet("user/{userId}")]
    [SwaggerOperation(Summary = "Все категории пользователя", Description = "Только активные")]
    public async Task<ActionResult<IEnumerable<CategoryDto>>> GetByUser(long userId)
    {
        var categories = await categoryService.GetUserCategoriesAsync(userId);
        return Ok(categories.Select(CategoryMapper.ToDto));
    }

    [HttpGet("user/{userId}/type/{type}")]
    [SwaggerOperation(Summary = "По типу", Description = "Income или Expense")]
    public async Task<ActionResult<IEnumerable<CategoryDto>>> GetByType(long userId, TransactionType type)
    {
        var categories = await categoryService.GetByTypeAsync(userId, type);
        return Ok(categories.Select(CategoryMapper.ToDto));
    }

    [HttpGet("{categoryId}/user/{userId}")]
    [SwaggerOperation(Summary = "По ID")]
    public async Task<ActionResult<CategoryDto>> GetById(long userId, int categoryId)
    {
        var c = await categoryService.GetCategoryByIdAsync(userId, categoryId);
        return c != null ? Ok(CategoryMapper.ToDto(c)) : NotFound();
    }

    [HttpGet("user/{userId}/search")]
    [SwaggerOperation(Summary = "Поиск по имени")]
    public async Task<ActionResult<CategoryDto>> GetByName(long userId, [FromQuery] string name)
    {
        var c = await categoryService.GetByNameAsync(userId, name);
        return c != null ? Ok(CategoryMapper.ToDto(c)) : NotFound();
    }

    // === CREATE ===

    [HttpPost("user/{userId}")]
    [SwaggerOperation(Summary = "Создать категорию")]
    public async Task<ActionResult<CategoryDto>> Create(
        long userId,
        [FromQuery] string name,
        [FromQuery] TransactionType type,
        [FromQuery] string icon = "📁")
    {
        var c = await categoryService.CreateAsync(userId, name, type, icon);
        return Ok(CategoryMapper.ToDto(c));
    }

    [HttpPost("user/{userId}/init")]
    [SwaggerOperation(Summary = "Инициализировать стандартные", Description = "Создаёт набор базовых категорий")]
    public async Task<ActionResult> InitDefaults(long userId)
    {
        await categoryService.InitializeDefaultCategoriesAsync(userId);
        return Ok(new { Message = "Категории созданы" });
    }

    // === UPDATE ===

    [HttpPut("{categoryId}/user/{userId}")]
    [SwaggerOperation(Summary = "Обновить категорию", Description = "Имя, иконка, приоритет")]
    public async Task<ActionResult<CategoryDto>> Update(
        long userId,
        int categoryId,
        [FromQuery] string name,
        [FromQuery] string? icon,
        [FromQuery] Priority priority)
    {
        var c = await categoryService.UpdateAsync(userId, categoryId, name, icon, priority);
        return c != null ? Ok(CategoryMapper.ToDto(c)) : NotFound();
    }

    [HttpPut("{categoryId}/user/{userId}/rename")]
    [SwaggerOperation(Summary = "Переименовать")]
    public async Task<ActionResult<CategoryDto>> Rename(long userId, int categoryId, [FromQuery] string newName)
    {
        var c = await categoryService.RenameAsync(userId, categoryId, newName);
        return c != null ? Ok(CategoryMapper.ToDto(c)) : NotFound();
    }

    // === ARCHIVE ===

    [HttpPut("{categoryId}/user/{userId}/archive")]
    [SwaggerOperation(Summary = "Архивировать", Description = "IsActive = false")]
    public async Task<ActionResult<CategoryDto>> Archive(long userId, int categoryId)
    {
        var c = await categoryService.ArchiveAsync(userId, categoryId);
        return c != null ? Ok(CategoryMapper.ToDto(c)) : NotFound();
    }

    [HttpPut("{categoryId}/user/{userId}/restore")]
    [SwaggerOperation(Summary = "Восстановить", Description = "IsActive = true")]
    public async Task<ActionResult<CategoryDto>> Restore(long userId, int categoryId)
    {
        var c = await categoryService.RestoreAsync(userId, categoryId);
        return c != null ? Ok(CategoryMapper.ToDto(c)) : NotFound();
    }

    // === DELETE ===

    [HttpDelete("{categoryId}/user/{userId}")]
    [SwaggerOperation(Summary = "Удалить", Description = "Архивирует категорию")]
    public async Task<ActionResult> Delete(long userId, int categoryId)
    {
        var result = await categoryService.DeleteAsync(userId, categoryId);
        return result ? Ok(new { Message = "Категория архивирована" }) : NotFound();
    }
}
