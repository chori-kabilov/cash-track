using Microsoft.AspNetCore.Mvc;
using Infrastructure.Services;
using Domain.DTOs;
using Domain.Enums;

namespace WebApi.Controllers;

// Контроллер транзакций
[ApiController]
[Route("api/transactions")]
public class TransactionsController(ITransactionService transactionService) : ControllerBase
{
    // Получить транзакции пользователя
    [HttpGet("user/{userId}")]
    public async Task<ActionResult<IEnumerable<TransactionDto>>> GetByUser(long userId, [FromQuery] int limit = 50)
    {
        var transactions = await transactionService.GetUserTransactionsAsync(userId, limit);
        
        var dtos = transactions.Select(t => new TransactionDto(
            t.Id,
            t.Amount,
            t.Type,
            t.Description,
            t.IsImpulsive,
            t.Date,
            t.Category != null ? new CategoryDto(
                t.Category.Id,
                t.Category.Name,
                t.Category.Icon,
                t.Category.Type,
                t.Category.Priority,
                t.Category.IsActive
            ) : null
        ));
        
        return Ok(dtos);
    }

    // Получить последнюю транзакцию
    [HttpGet("user/{userId}/last")]
    public async Task<ActionResult<TransactionDto>> GetLast(long userId)
    {
        var t = await transactionService.GetLastTransactionAsync(userId);
        if (t == null) return NotFound();
        
        return Ok(new TransactionDto(
            t.Id, t.Amount, t.Type, t.Description, t.IsImpulsive, t.Date,
            t.Category != null ? new CategoryDto(t.Category.Id, t.Category.Name, t.Category.Icon, t.Category.Type, t.Category.Priority, t.Category.IsActive) : null
        ));
    }

    // Статистика за период
    [HttpGet("user/{userId}/stats")]
    public async Task<ActionResult<TransactionStatsDto>> GetStats(long userId, [FromQuery] int days = 30)
    {
        var from = DateTimeOffset.UtcNow.AddDays(-days);
        
        var income = await transactionService.GetTotalIncomeAsync(userId, from);
        var expense = await transactionService.GetTotalExpenseAsync(userId, from);
        var topExpenses = await transactionService.GetTopExpensesAsync(userId, from, 5);
        var transactions = await transactionService.GetUserTransactionsAsync(userId, 1000);
        var count = transactions.Count(t => t.Date >= from);
        
        return Ok(new TransactionStatsDto(
            income,
            expense,
            income - expense,
            count,
            topExpenses.Select(x => new CategoryExpenseDto(x.Category.Name, x.Category.Icon, x.Amount))
        ));
    }

    // Создать транзакцию
    [HttpPost("user/{userId}")]
    public async Task<ActionResult<TransactionDto>> Create(
        long userId,
        [FromQuery] int categoryId,
        [FromQuery] decimal amount,
        [FromQuery] TransactionType type,
        [FromQuery] string? description = null,
        [FromQuery] bool isImpulsive = false)
    {
        var t = await transactionService.ProcessTransactionAsync(
            userId, categoryId, amount, type, description, isImpulsive);
        
        return Ok(new TransactionDto(
            t.Id, t.Amount, t.Type, t.Description, t.IsImpulsive, t.Date,
            t.Category != null ? new CategoryDto(t.Category.Id, t.Category.Name, t.Category.Icon, t.Category.Type, t.Category.Priority, t.Category.IsActive) : null
        ));
    }
}
