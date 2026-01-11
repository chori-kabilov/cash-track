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
    ITransactionService transactionService) : ICallbackHandler
{
    public async Task<bool> HandleAsync(ITelegramBotClient bot, CallbackQuery cb, string data, UserFlowState? flow, Dictionary<long, UserFlowState> flowDict, CancellationToken ct)
    {
        if (!data.StartsWith("stat:")) return false;

        var userId = cb.From.Id;
        var chatId = cb.Message!.Chat.Id;
        var msgId = cb.Message.MessageId;
        
        // Инициализируем flow для статистики
        if (!flowDict.TryGetValue(userId, out var sFlow))
        {
            sFlow = new UserFlowState();
            flowDict[userId] = sFlow;
        }

        switch (data)
        {
            case "stat:summary":
                sFlow.CurrentStatsScreen = StatsScreen.Summary;
                await statsCmd.RenderCurrentScreenAsync(bot, chatId, userId, sFlow, ct, msgId);
                return true;
            case "stat:categories":
                sFlow.CurrentStatsScreen = StatsScreen.Categories;
                await statsCmd.RenderCurrentScreenAsync(bot, chatId, userId, sFlow, ct, msgId);
                return true;
            case "stat:history":
                sFlow.CurrentStatsScreen = StatsScreen.History;
                sFlow.StatsPage = 1;
                await statsCmd.RenderCurrentScreenAsync(bot, chatId, userId, sFlow, ct, msgId);
                return true;
            case "stat:emotions":
                sFlow.CurrentStatsScreen = StatsScreen.Emotions;
                await statsCmd.RenderCurrentScreenAsync(bot, chatId, userId, sFlow, ct, msgId);
                return true;
            case "stat:regular":
                sFlow.CurrentStatsScreen = StatsScreen.Regular;
                await statsCmd.RenderCurrentScreenAsync(bot, chatId, userId, sFlow, ct, msgId);
                return true;
            case "stat:period":
                sFlow.CurrentStatsScreen = StatsScreen.PeriodSelect;
                await statsCmd.RenderCurrentScreenAsync(bot, chatId, userId, sFlow, ct, msgId);
                return true;
            case "stat:prev":
                sFlow.StatsDate = sFlow.StatsPeriod switch
                {
                    StatsPeriod.Week => sFlow.StatsDate.AddDays(-7),
                    StatsPeriod.Month => sFlow.StatsDate.AddMonths(-1),
                    StatsPeriod.Year => sFlow.StatsDate.AddYears(-1),
                    _ => sFlow.StatsDate.AddMonths(-1)
                };
                await statsCmd.RenderCurrentScreenAsync(bot, chatId, userId, sFlow, ct, msgId);
                return true;
            case "stat:next":
                sFlow.StatsDate = sFlow.StatsPeriod switch
                {
                    StatsPeriod.Week => sFlow.StatsDate.AddDays(7),
                    StatsPeriod.Month => sFlow.StatsDate.AddMonths(1),
                    StatsPeriod.Year => sFlow.StatsDate.AddYears(1),
                    _ => sFlow.StatsDate.AddMonths(1)
                };
                await statsCmd.RenderCurrentScreenAsync(bot, chatId, userId, sFlow, ct, msgId);
                return true;
            case "stat:period:week":
                sFlow.StatsPeriod = StatsPeriod.Week;
                sFlow.CurrentStatsScreen = StatsScreen.Summary;
                await statsCmd.RenderCurrentScreenAsync(bot, chatId, userId, sFlow, ct, msgId);
                return true;
            case "stat:period:month":
                sFlow.StatsPeriod = StatsPeriod.Month;
                sFlow.CurrentStatsScreen = StatsScreen.Summary;
                await statsCmd.RenderCurrentScreenAsync(bot, chatId, userId, sFlow, ct, msgId);
                return true;
            case "stat:period:year":
                sFlow.StatsPeriod = StatsPeriod.Year;
                sFlow.CurrentStatsScreen = StatsScreen.Summary;
                await statsCmd.RenderCurrentScreenAsync(bot, chatId, userId, sFlow, ct, msgId);
                return true;
            case "stat:cat:exp":
                sFlow.StatsShowExpenses = true;
                await statsCmd.RenderCurrentScreenAsync(bot, chatId, userId, sFlow, ct, msgId);
                return true;
            case "stat:cat:inc":
                sFlow.StatsShowExpenses = false;
                await statsCmd.RenderCurrentScreenAsync(bot, chatId, userId, sFlow, ct, msgId);
                return true;
            case "stat:page:prev":
                if (sFlow.StatsPage > 1) sFlow.StatsPage--;
                await statsCmd.RenderCurrentScreenAsync(bot, chatId, userId, sFlow, ct, msgId);
                return true;
            case "stat:page:next":
                sFlow.StatsPage++;
                await statsCmd.RenderCurrentScreenAsync(bot, chatId, userId, sFlow, ct, msgId);
                return true;
            case "stat:back":
                flowDict.Remove(userId);
                await bot.EditMessageTextAsync(chatId, msgId, "🏠 *Главное меню*\n\nВыберите действие:", 
                    replyMarkup: BotInlineKeyboards.MainMenu(), cancellationToken: ct);
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
