using Domain.Entities;
using Infrastructure.Data;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;

namespace Console.Services;

public class SchedulerService
{
    private readonly ITelegramBotClient _botClient;
    private readonly DbContextOptions<DataContext> _dbOptions;
    private readonly CancellationTokenSource _cts = new();

    public SchedulerService(ITelegramBotClient botClient, DbContextOptions<DataContext> dbOptions)
    {
        _botClient = botClient;
        _dbOptions = dbOptions;
    }

    public void Start()
    {
        Task.Run(() => RunLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts.Cancel();
    }

    private async Task RunLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var now = DateTimeOffset.UtcNow;
                // Run only if it is between 9:00 and 10:00 AM UTC (approx morning)
                // Or just run every check and keep track of "LastSent"?
                // MVP: Run every hour, check if we need to send reminders.
                // To avoid spam, we need to know if we already sent reminder today.
                // BUT we don't store "LastReminderSent" in DB for all items.
                // RegularPayments has "ReminderDaysBefore".
                
                await CheckRemindersAsync(token);
                
                // Sleep 1 hour
                await Task.Delay(TimeSpan.FromHours(1), token);
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Scheduler Error: {ex.Message}");
                await Task.Delay(TimeSpan.FromMinutes(5), token);
            }
        }
    }

    private async Task CheckRemindersAsync(CancellationToken token)
    {
        using var context = new DataContext(_dbOptions);
        var regularService = new RegularPaymentService(context);
        var debtService = new DebtService(context);
        var userService = new UserService(context); // To get user chat ID if needed? 
        // Note: Telegram User ID is usually Chat ID for private chats.
        // User entity has Id which is Telegram ID.

        // 1. Regular Payments
        var users = await context.Users.Select(u => u.Id).ToListAsync(token);

        foreach (var userId in users)
        {
            try 
            {
                // We need to pass options so we use CreateAsync with provided options?
                // Services expect DataContext.
                
                var duePayments = await regularService.GetDuePaymentsAsync(userId, token);
                foreach (var p in duePayments)
                {
                    // Check if we should send reminder?
                    // GetDuePaymentsAsync returns payments where (NextDate - ReminderDays) <= Now.
                    // We need to avoid spamming every hour.
                    // We can check if hour is 9 AM? 
                    // Let's assume we run this check daily or we enforce hour check.
                    
                    if (DateTimeOffset.UtcNow.Hour == 9) // UTC 9 AM
                    {
                         await _botClient.SendTextMessageAsync(userId, $"🔔 *Напоминание*: Платеж \"{p.Name}\" ({p.Amount:F2}) ожидает оплаты {p.NextDueDate:dd.MM.yyyy}", Telegram.Bot.Types.Enums.ParseMode.Markdown, cancellationToken: token);
                    }
                }

                // 2. Overdue Debts
                if (DateTimeOffset.UtcNow.Hour == 9)
                {
                    var overdueDebts = await debtService.GetOverdueDebtsAsync(userId, token);
                    foreach (var d in overdueDebts)
                    {
                         // Ask to remind only once? or Daily?
                         // Daily reminder for overdue.
                         await _botClient.SendTextMessageAsync(userId, $"⚠️ *Просроченный долг*: {d.PersonName} ({d.Amount - d.RemainingAmount}/{d.Amount}) был должен до {d.DueDate:dd.MM.yyyy}", Telegram.Bot.Types.Enums.ParseMode.Markdown, cancellationToken: token);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error processing user {userId}: {ex.Message}");
            }
        }
    }
}
