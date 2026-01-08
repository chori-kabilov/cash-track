using Infrastructure.Data;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace Console.Services;

public class SchedulerService(ITelegramBotClient botClient, DbContextOptions<DataContext> dbOptions)
{
    private readonly CancellationTokenSource _cts = new();

    public void Start() => Task.Run(() => RunLoopAsync(_cts.Token));
    public void Stop() => _cts.Cancel();

    private async Task RunLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                // Проверяем напоминания только в 9:00 UTC (14:00 по Душанбе)
                if (DateTimeOffset.UtcNow.Hour == 9)
                    await CheckRemindersAsync(token);
                
                await Task.Delay(TimeSpan.FromHours(1), token);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Scheduler: {ex.Message}");
                await Task.Delay(TimeSpan.FromMinutes(5), token);
            }
        }
    }

    private async Task CheckRemindersAsync(CancellationToken token)
    {
        using var context = new DataContext(dbOptions);
        var regularService = new RegularPaymentService(context);
        var debtService = new DebtService(context);

        var userIds = await context.Users.Where(u => !u.IsDeleted).Select(u => u.Id).ToListAsync(token);

        foreach (var userId in userIds)
        {
            try
            {
                // Регулярные платежи
                var duePayments = await regularService.GetDuePaymentsAsync(userId, token);
                foreach (var p in duePayments)
                    await SendAsync(userId, $"🔔 Платеж \"{p.Name}\" ({p.Amount:F2}) — {p.NextDueDate:dd.MM}", token);

                // Просроченные долги
                var overdueDebts = await debtService.GetOverdueDebtsAsync(userId, token);
                foreach (var d in overdueDebts)
                    await SendAsync(userId, $"⚠️ Долг: {d.PersonName} — просрочен с {d.DueDate:dd.MM}", token);
            }
            catch { /* Пропускаем ошибки отдельных пользователей */ }
        }
    }

    private Task SendAsync(long chatId, string text, CancellationToken token) =>
        botClient.SendTextMessageAsync(chatId, text, ParseMode.Markdown, cancellationToken: token);
}
