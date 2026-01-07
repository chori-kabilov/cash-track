using Microsoft.AspNetCore.Mvc;
using Infrastructure.Services;
using Domain.DTOs;

namespace WebApi.Controllers;

// Контроллер счетов
[ApiController]
[Route("api/accounts")]
public class AccountsController(IAccountService accountService) : ControllerBase
{
    // Получить счёт пользователя
    [HttpGet("user/{userId}")]
    public async Task<ActionResult<AccountDto>> GetByUserId(long userId)
    {
        var account = await accountService.GetUserAccountAsync(userId);
        if (account == null) return NotFound();
        
        return Ok(new AccountDto(
            account.Id,
            account.UserId,
            account.Name,
            account.Balance,
            account.Currency,
            account.UpdatedAt
        ));
    }

    // Получить баланс
    [HttpGet("user/{userId}/balance")]
    public async Task<ActionResult<object>> GetBalance(long userId)
    {
        var account = await accountService.GetUserAccountAsync(userId);
        if (account == null) return NotFound();
        return Ok(new { account.Balance, account.Currency });
    }

    // Создать счёт
    [HttpPost("user/{userId}")]
    public async Task<ActionResult<AccountDto>> Create(long userId, [FromQuery] string name = "Основной счёт")
    {
        var account = await accountService.CreateAccountAsync(userId, name);
        return Ok(new AccountDto(
            account.Id,
            account.UserId,
            account.Name,
            account.Balance,
            account.Currency,
            account.UpdatedAt
        ));
    }

    // Обновить баланс
    [HttpPut("{accountId}/balance")]
    public async Task<ActionResult> UpdateBalance(int accountId, [FromQuery] decimal amount)
    {
        await accountService.UpdateBalanceAsync(accountId, amount);
        return Ok(new { Message = "Баланс обновлён", Amount = amount });
    }
}
