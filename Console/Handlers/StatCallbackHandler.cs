using System.Text;
using Console.Bot;
using Console.Commands;
using Console.Flow;
using Domain.Enums;
using Infrastructure.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.InputFiles;

namespace Console.Handlers;

public class StatCallbackHandler(
    StatsCommand statsCmd,
    ITransactionService transactionService,
    IAccountService accountService) : ICallbackHandler
{
    public async Task<bool> HandleAsync(ITelegramBotClient bot, CallbackQuery cb, string data, UserFlowState? flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(data) || !data.StartsWith("stat:")) return false;
        data = data.Trim();

        System.Console.WriteLine($"[StatHandler] Received: {data}"); // DEBUG

        var userId = cb.From.Id;
        var chatId = cb.Message!.Chat.Id;
        var msgId = cb.Message.MessageId;
        
        // Инициализируем flow для статистики
        if (!flowDict.TryGetValue(userId, out var sFlow))
        {
            sFlow = new UserFlowState();
            flowDict[userId] = sFlow;
            System.Console.WriteLine($"[StatHandler] Created new flow for {userId}"); // DEBUG
        }
        else
        {
             System.Console.WriteLine($"[StatHandler] Using existing flow. Screen: {sFlow.CurrentStatsScreen}"); // DEBUG
        }

        // Получаем дату регистрации для ограничений
        var account = await accountService.GetUserAccountAsync(userId, ct);
        var regDate = account?.CreatedAt ?? DateTimeOffset.MinValue;
        var now = DateTimeOffset.UtcNow;

        switch (data)
        {
            // ... (cases)

            case "stat:prev": // Назад в прошлое
                // Если выбран период "За все время", навигация не нужна
                if (sFlow.StatsPeriod == StatsPeriod.AllTime) return true;

                var prevDate = sFlow.StatsPeriod switch
                {
                    StatsPeriod.Week => sFlow.StatsDate.AddDays(-7),
                    StatsPeriod.Month => sFlow.StatsDate.AddMonths(-1),
                    StatsPeriod.Year => sFlow.StatsDate.AddYears(-1),
                    _ => sFlow.StatsDate.AddMonths(-1)
                };
                
                // Строгая проверка "не раньше регистрации"
                var minDate = sFlow.StatsPeriod switch
                {
                    StatsPeriod.Week => regDate, 
                    StatsPeriod.Month => new DateTimeOffset(regDate.Year, regDate.Month, 1, 0, 0, 0, regDate.Offset),
                    StatsPeriod.Year => new DateTimeOffset(regDate.Year, 1, 1, 0, 0, 0, regDate.Offset),
                    _ => regDate
                };

                // Если новая дата попадает в период, который РАНЬШЕ минимального периода (не включительно), то стоп.
                // То есть, если мы сейчас на Январе 2024 (дата регистрации), то prevDate будет Декабрь 2023 -> нельзя.
                // Проверяем по дате (без времени), чтобы не блокировать, если время регистрации позже текущего времени
                if (prevDate.Date < minDate.Date) 
                {
                     return true; 
                }
                
                sFlow.StatsDate = prevDate;
                await statsCmd.RenderCurrentScreenAsync(bot, chatId, userId, sFlow, ct, msgId, cb.Id);
                return true;

            case "stat:next": // Вперед в будущее
                // Если выбран период "За все время", навигация не нужна
                if (sFlow.StatsPeriod == StatsPeriod.AllTime) return true;

                var nextDate = sFlow.StatsPeriod switch
                {
                    StatsPeriod.Week => sFlow.StatsDate.AddDays(7),
                    StatsPeriod.Month => sFlow.StatsDate.AddMonths(1),
                    StatsPeriod.Year => sFlow.StatsDate.AddYears(1),
                    _ => sFlow.StatsDate.AddMonths(1)
                };

                // Строгая проверка "не позже текущей даты"
                // Если начало следующего периода БОЛЬШЕ чем сейчас -> нельзя.
                // Например, сейчас 15 Января. Мы смотрим Январь. nextDate = Февраль (с 1 числа). 
                // 1 Февраля > 15 Января -> нельзя.
                // Для недели: сейчас Среда (15-е). Текущая неделя (13-19). nextDate = (20-26).
                // 20-е > 15-го -> нельзя.
                
                // Получаем начало следующего периода
                DateTimeOffset nextPeriodStart = nextDate; // По умолчанию flow.Date это и есть какая-то точка в периоде, но для точности:
                if (sFlow.StatsPeriod == StatsPeriod.Month) nextPeriodStart = new DateTimeOffset(nextDate.Year, nextDate.Month, 1, 0,0,0, nextDate.Offset);
                if (sFlow.StatsPeriod == StatsPeriod.Year) nextPeriodStart = new DateTimeOffset(nextDate.Year, 1, 1, 0,0,0, nextDate.Offset);
                if (sFlow.StatsPeriod == StatsPeriod.Week) nextPeriodStart = nextDate; // Для кастомной недели просто проверяем саму дату
                
                // Проверяем по дате
                if (nextPeriodStart.Date > now.Date)
                {
                     return true; 
                }

                sFlow.StatsDate = nextDate;
                await statsCmd.RenderCurrentScreenAsync(bot, chatId, userId, sFlow, ct, msgId, cb.Id);
                return true;

            // ... (other cases)
            case "stat:summary":
                System.Console.WriteLine("[StatHandler] Case: stat:summary");
                sFlow.CurrentStatsScreen = StatsScreen.Summary;
                await statsCmd.RenderCurrentScreenAsync(bot, chatId, userId, sFlow, ct, msgId, cb.Id);
                return true;
            case "stat:categories":
                System.Console.WriteLine("[StatHandler] Case: stat:categories");
                sFlow.CurrentStatsScreen = StatsScreen.Categories;
                await statsCmd.RenderCurrentScreenAsync(bot, chatId, userId, sFlow, ct, msgId, cb.Id);
                return true;
            case "stat:history":
                System.Console.WriteLine("[StatHandler] Case: stat:history");
                sFlow.CurrentStatsScreen = StatsScreen.History;
                sFlow.StatsPage = 1;
                await statsCmd.RenderCurrentScreenAsync(bot, chatId, userId, sFlow, ct, msgId, cb.Id);
                return true;
            case "stat:emotions":
                System.Console.WriteLine("[StatHandler] Case: stat:emotions");
                sFlow.CurrentStatsScreen = StatsScreen.Emotions;
                await statsCmd.RenderCurrentScreenAsync(bot, chatId, userId, sFlow, ct, msgId, cb.Id);
                return true;
            case "stat:regular":
                System.Console.WriteLine("[StatHandler] Case: stat:regular");
                sFlow.CurrentStatsScreen = StatsScreen.Regular;
                await statsCmd.RenderCurrentScreenAsync(bot, chatId, userId, sFlow, ct, msgId, cb.Id);
                return true;
            case "stat:period":
                System.Console.WriteLine("[StatHandler] Case: stat:period");
                sFlow.CurrentStatsScreen = StatsScreen.PeriodSelect;
                await statsCmd.RenderCurrentScreenAsync(bot, chatId, userId, sFlow, ct, msgId, cb.Id);
                return true;
            case "stat:period:week":
                System.Console.WriteLine("[StatHandler] Case: stat:period:week");
                sFlow.StatsPeriod = StatsPeriod.Week;
                sFlow.CurrentStatsScreen = StatsScreen.Summary;
                await statsCmd.RenderCurrentScreenAsync(bot, chatId, userId, sFlow, ct, msgId, cb.Id);
                return true;
            case "stat:period:month":
                System.Console.WriteLine("[StatHandler] Case: stat:period:month");
                sFlow.StatsPeriod = StatsPeriod.Month;
                sFlow.CurrentStatsScreen = StatsScreen.Summary;
                await statsCmd.RenderCurrentScreenAsync(bot, chatId, userId, sFlow, ct, msgId, cb.Id);
                return true;
            case "stat:period:year":
                System.Console.WriteLine("[StatHandler] Case: stat:period:year");
                sFlow.StatsPeriod = StatsPeriod.Year;
                sFlow.CurrentStatsScreen = StatsScreen.Summary;
                await statsCmd.RenderCurrentScreenAsync(bot, chatId, userId, sFlow, ct, msgId, cb.Id);
                return true;
            case "stat:period:all":
                System.Console.WriteLine("[StatHandler] Case: stat:period:all");
                sFlow.StatsPeriod = StatsPeriod.AllTime;
                sFlow.CurrentStatsScreen = StatsScreen.Summary;
                await statsCmd.RenderCurrentScreenAsync(bot, chatId, userId, sFlow, ct, msgId, cb.Id);
                return true;
            case "stat:cat:exp":
                System.Console.WriteLine("[StatHandler] Case: stat:cat:exp");
                sFlow.StatsShowExpenses = true;
                await statsCmd.RenderCurrentScreenAsync(bot, chatId, userId, sFlow, ct, msgId, cb.Id);
                return true;
            case "stat:cat:inc":
                System.Console.WriteLine("[StatHandler] Case: stat:cat:inc");
                sFlow.StatsShowExpenses = false;
                await statsCmd.RenderCurrentScreenAsync(bot, chatId, userId, sFlow, ct, msgId, cb.Id);
                return true;
            case "stat:page:prev":
                if (sFlow.StatsPage > 1) sFlow.StatsPage--;
                await statsCmd.RenderCurrentScreenAsync(bot, chatId, userId, sFlow, ct, msgId, cb.Id);
                return true;
            case "stat:page:next":
                sFlow.StatsPage++;
                await statsCmd.RenderCurrentScreenAsync(bot, chatId, userId, sFlow, ct, msgId, cb.Id);
                return true;
            case "stat:back":
                flowDict.Remove(userId);
                await CommandHelpers.SafeEditMessageAsync(bot, chatId, msgId, "🏠 Главное меню\n\nВыберите действие:", 
                    BotInlineKeyboards.MainMenu(), ct, cb.Id);
                return true;
            case "stat:export":
                var csv = await GenerateCsvAsync(userId, sFlow, ct);
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv)))
                {
                    var fileName = $"CashTrack_{sFlow.StatsDate:yyyy_MM}.csv";
                    await bot.SendDocumentAsync(chatId, 
                        new InputOnlineFile(stream, fileName), 
                        caption: "📄 Ваш финансовый отчет", cancellationToken: ct);
                }
                return true;
            case "stat:noop":
                return true;
        }

        return false;
    }

    private async Task<string> GenerateCsvAsync(long userId, UserFlowState flow, CancellationToken ct)
    {
        var date = flow.StatsDate;
        var from = flow.StatsPeriod switch
        {
            StatsPeriod.Week => date.AddDays(-(int)date.DayOfWeek + 1),
            StatsPeriod.Month => new DateTimeOffset(date.Year, date.Month, 1, 0, 0, 0, date.Offset),
            StatsPeriod.Year => new DateTimeOffset(date.Year, 1, 1, 0, 0, 0, date.Offset),
            _ => date.AddDays(-30)
        };
        var to = flow.StatsPeriod switch
        {
            StatsPeriod.Week => date.AddDays(7 - (int)date.DayOfWeek),
            StatsPeriod.Month => new DateTimeOffset(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month), 23, 59, 59, date.Offset),
            StatsPeriod.Year => new DateTimeOffset(date.Year, 12, 31, 23, 59, 59, date.Offset),
            _ => date
        };

        var transactions = await transactionService.GetUserTransactionsAsync(userId, 1000, ct);
        var filtered = transactions.Where(t => t.Date >= from && t.Date <= to).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("Дата,Тип,Категория,Сумма,Описание,На эмоциях");
        foreach (var t in filtered)
        {
            var type = t.Type == TransactionType.Income ? "Доход" : "Расход";
            var cat = t.Category?.Name ?? "";
            var desc = t.Description?.Replace(",", " ") ?? "";
            var emo = t.IsImpulsive ? "Да" : "Нет";
            sb.AppendLine($"{t.Date:dd.MM.yyyy},{type},{cat},{t.Amount:F2},{desc},{emo}");
        }
        return sb.ToString();
    }
}
