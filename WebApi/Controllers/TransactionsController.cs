using Microsoft.AspNetCore.Mvc;
using Infrastructure.Services;
using Infrastructure.Mappers;
using Domain.DTOs;
using Domain.Enums;
using Swashbuckle.AspNetCore.Annotations;

namespace WebApi.Controllers;

// Контроллер транзакций
[ApiController]
[Route("api/transactions")]
[SwaggerTag("Управление транзакциями")]
public class TransactionsController(ITransactionService transactionService) : ControllerBase
{
    // === READ ===

    [HttpGet("{id}")]
    [SwaggerOperation(Summary = "Получить транзакцию по ID")]
    public async Task<ActionResult<TransactionDto>> GetById(int id)
    {
        var t = await transactionService.GetByIdAsync(id);
        return t != null ? Ok(TransactionMapper.ToDto(t)) : NotFound();
    }

    [HttpGet("user/{userId}")]
    [SwaggerOperation(Summary = "Получить транзакции пользователя", Description = "Последние N транзакций")]
    public async Task<ActionResult<IEnumerable<TransactionDto>>> GetByUser(long userId, [FromQuery] int limit = 50)
    {
        var transactions = await transactionService.GetUserTransactionsAsync(userId, limit);
        return Ok(transactions.Select(TransactionMapper.ToDto));
    }

    [HttpGet("user/{userId}/last")]
    [SwaggerOperation(Summary = "Последняя транзакция")]
    public async Task<ActionResult<TransactionDto>> GetLast(long userId)
    {
        var t = await transactionService.GetLastTransactionAsync(userId);
        return t != null ? Ok(TransactionMapper.ToDto(t)) : NotFound();
    }

    [HttpGet("user/{userId}/category/{categoryId}")]
    [SwaggerOperation(Summary = "Транзакции по категории")]
    public async Task<ActionResult<IEnumerable<TransactionDto>>> GetByCategory(long userId, int categoryId, [FromQuery] int limit = 50)
    {
        var transactions = await transactionService.GetByCategoryAsync(userId, categoryId, limit);
        return Ok(transactions.Select(TransactionMapper.ToDto));
    }

    [HttpGet("user/{userId}/paged")]
    [SwaggerOperation(Summary = "Транзакции с пагинацией", Description = "Для истории с фильтрами")]
    public async Task<ActionResult<object>> GetPaged(
        long userId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] TransactionType? type = null,
        [FromQuery] DateTimeOffset? from = null,
        [FromQuery] string? search = null)
    {
        var (items, total) = await transactionService.GetTransactionsPageAsync(userId, page, pageSize, type, from, search);
        return Ok(new
        {
            Items = items.Select(TransactionMapper.ToDto),
            TotalCount = total,
            Page = page,
            PageSize = pageSize,
            TotalPages = (int)Math.Ceiling(total / (double)pageSize)
        });
    }

    [HttpGet("user/{userId}/stats")]
    [SwaggerOperation(Summary = "Статистика за период", Description = "Доходы, расходы, топ категорий")]
    public async Task<ActionResult<TransactionStatsDto>> GetStats(long userId, [FromQuery] int days = 30)
    {
        var from = DateTimeOffset.UtcNow.AddDays(-days);
        
        var income = await transactionService.GetTotalIncomeAsync(userId, from);
        var expense = await transactionService.GetTotalExpenseAsync(userId, from);
        var topExpenses = await transactionService.GetTopExpensesAsync(userId, from, 5);
        var (_, count) = await transactionService.GetTransactionsPageAsync(userId, 1, 1, null, from);
        
        return Ok(new TransactionStatsDto(
            income,
            expense,
            income - expense,
            count,
            topExpenses.Select(x => new CategoryExpenseDto(x.Category.Name, x.Category.Icon, x.Amount))
        ));
    }

    // === CREATE ===

    [HttpPost("user/{userId}")]
    [SwaggerOperation(Summary = "Создать транзакцию", Description = "Автоматически обновляет баланс")]
    public async Task<ActionResult<TransactionDto>> Create(
        long userId,
        [FromQuery] int categoryId,
        [FromQuery] decimal amount,
        [FromQuery] TransactionType type,
        [FromQuery] string? description = null,
        [FromQuery] bool isImpulsive = false)
    {
        try
        {
            var t = await transactionService.ProcessTransactionAsync(userId, categoryId, amount, type, description, isImpulsive);
            return Ok(TransactionMapper.ToDto(t));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Error = ex.Message });
        }
    }

    // === UPDATE ===

    [HttpPut("{id}")]
    [SwaggerOperation(Summary = "Обновить описание", Description = "Изменить только описание транзакции")]
    public async Task<ActionResult<TransactionDto>> UpdateDescription(int id, [FromQuery] string? description)
    {
        var t = await transactionService.UpdateDescriptionAsync(id, description);
        return t != null ? Ok(TransactionMapper.ToDto(t)) : NotFound();
    }

    [HttpPost("{id}/cancel")]
    [SwaggerOperation(Summary = "Отменить транзакцию", Description = "Пометить как ошибочную и вернуть деньги")]
    public async Task<ActionResult<TransactionDto>> Cancel(int id)
    {
        var t = await transactionService.CancelAsync(id);
        return t != null ? Ok(TransactionMapper.ToDto(t)) : NotFound();
    }

    // === DELETE ===

    [HttpDelete("{id}")]
    [SwaggerOperation(Summary = "Удалить транзакцию", Description = "Мягкое удаление (soft-delete)")]
    public async Task<ActionResult> Delete(int id)
    {
        var result = await transactionService.DeleteAsync(id);
        return result ? Ok(new { Message = "Транзакция удалена" }) : NotFound();
    }
}
