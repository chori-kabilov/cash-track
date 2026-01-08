using Microsoft.AspNetCore.Mvc;
using Infrastructure.Services;
using Infrastructure.Mappers;
using Domain.DTOs;
using Swashbuckle.AspNetCore.Annotations;

namespace WebApi.Controllers;

// Контроллер счетов
[ApiController]
[Route("api/accounts")]
[SwaggerTag("Управление счетами пользователей")]
public class AccountsController(IAccountService accountService) : ControllerBase
{
    // === READ ===
    
    [HttpGet]
    [SwaggerOperation(Summary = "Получить все счета", Description = "Возвращает список всех активных счетов")]
    public async Task<ActionResult<IEnumerable<AccountDto>>> GetAll()
    {
        var accounts = await accountService.GetAllAsync();
        return Ok(accounts.Select(AccountMapper.ToDto));
    }

    [HttpGet("{id}")]
    [SwaggerOperation(Summary = "Получить счёт по ID", Description = "Возвращает счёт по уникальному идентификатору")]
    public async Task<ActionResult<AccountDto>> GetById(int id)
    {
        var account = await accountService.GetByIdAsync(id);
        return account != null ? Ok(AccountMapper.ToDto(account)) : NotFound();
    }

    [HttpGet("user/{userId}")]
    [SwaggerOperation(Summary = "Получить счёт пользователя", Description = "Возвращает счёт по Telegram ID пользователя")]
    public async Task<ActionResult<AccountDto>> GetByUserId(long userId)
    {
        var account = await accountService.GetUserAccountAsync(userId);
        return account != null ? Ok(AccountMapper.ToDto(account)) : NotFound();
    }

    [HttpGet("user/{userId}/balance")]
    [SwaggerOperation(Summary = "Получить баланс", Description = "Возвращает только баланс и валюту счёта")]
    public async Task<ActionResult<object>> GetBalance(long userId)
    {
        var account = await accountService.GetUserAccountAsync(userId);
        if (account == null) return NotFound();
        return Ok(new { account.Balance, account.Currency });
    }

    [HttpGet("user/{userId}/exists")]
    [SwaggerOperation(Summary = "Проверить наличие счёта", Description = "Проверяет существует ли счёт у пользователя")]
    public async Task<ActionResult<object>> Exists(long userId)
    {
        var exists = await accountService.ExistsAsync(userId);
        return Ok(new { UserId = userId, Exists = exists });
    }

    // === CREATE ===

    [HttpPost("user/{userId}")]
    [SwaggerOperation(Summary = "Создать счёт", Description = "Создаёт новый счёт для пользователя")]
    public async Task<ActionResult<AccountDto>> Create(long userId, [FromQuery] string name = "Основной счёт")
    {
        var account = await accountService.CreateAccountAsync(userId, name);
        return Ok(AccountMapper.ToDto(account));
    }

    // === UPDATE ===

    [HttpPut("{id}")]
    [SwaggerOperation(Summary = "Обновить счёт", Description = "Обновить название и валюту счёта")]
    public async Task<ActionResult<AccountDto>> Update(int id, [FromQuery] string name, [FromQuery] string currency)
    {
        var account = await accountService.UpdateAsync(id, name, currency);
        return account != null ? Ok(AccountMapper.ToDto(account)) : NotFound();
    }

    [HttpPut("{id}/balance")]
    [SwaggerOperation(Summary = "Установить баланс", Description = "Установить точное значение баланса")]
    public async Task<ActionResult<AccountDto>> SetBalance(int id, [FromQuery] decimal balance)
    {
        var account = await accountService.SetBalanceAsync(id, balance);
        return account != null ? Ok(AccountMapper.ToDto(account)) : NotFound();
    }

    // === BALANCE OPERATIONS ===

    [HttpPost("{id}/deposit")]
    [SwaggerOperation(Summary = "Пополнить счёт", Description = "Добавить указанную сумму к балансу")]
    public async Task<ActionResult<AccountDto>> Deposit(int id, [FromQuery] decimal amount)
    {
        if (amount <= 0) return BadRequest(new { Error = "Сумма должна быть > 0" });
        var account = await accountService.DepositAsync(id, amount);
        return account != null ? Ok(AccountMapper.ToDto(account)) : NotFound();
    }

    [HttpPost("{id}/withdraw")]
    [SwaggerOperation(Summary = "Списать со счёта", Description = "Списать указанную сумму с баланса")]
    public async Task<ActionResult<AccountDto>> Withdraw(int id, [FromQuery] decimal amount)
    {
        if (amount <= 0) return BadRequest(new { Error = "Сумма должна быть > 0" });
        var account = await accountService.WithdrawAsync(id, amount);
        return account != null ? Ok(AccountMapper.ToDto(account)) : NotFound();
    }

    // === DELETE ===

    [HttpDelete("{id}")]
    [SwaggerOperation(Summary = "Удалить счёт", Description = "Мягкое удаление (soft-delete) — помечает как удалённый")]
    public async Task<ActionResult> Delete(int id)
    {
        var result = await accountService.DeleteAsync(id);
        return result ? Ok(new { Message = "Счёт удалён" }) : NotFound();
    }
}
