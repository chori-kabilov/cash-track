using Console.Bot;
using Console.Flow;
using Domain.Enums;
using Infrastructure.Services;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace Console.Commands;

// Команда "Статистика" — хаб с несколькими экранами
public class StatsCommand(
    IAccountService accountService,
    ITransactionService transactionService,
    ICategoryService categoryService,
    ILimitService limitService,
    IRegularPaymentService regularPaymentService)
{
    // Главная точка входа
    public async Task ExecuteAsync(ITelegramBotClient bot, long chatId, long userId, 
        UserFlowState flow, CancellationToken ct, int? messageId = null)
    {
        flow.CurrentStatsScreen = StatsScreen.Summary;
        await RenderCurrentScreenAsync(bot, chatId, userId, flow, ct, messageId);
    }

    // Рендер текущего экрана на основе flow.CurrentStatsScreen
    public async Task RenderCurrentScreenAsync(ITelegramBotClient bot, long chatId, long userId,
        UserFlowState flow, CancellationToken ct, int? messageId = null)
    {
        var (text, keyboard) = flow.CurrentStatsScreen switch
        {
            StatsScreen.Summary => await BuildSummaryAsync(userId, flow, ct),
            StatsScreen.Categories => await BuildCategoriesAsync(userId, flow, ct),
            StatsScreen.History => await BuildHistoryAsync(userId, flow, ct),
            StatsScreen.Emotions => await BuildEmotionsAsync(userId, flow, ct),
            StatsScreen.Regular => await BuildRegularAsync(userId, flow, ct),
            StatsScreen.PeriodSelect => BuildPeriodSelect(flow),
            _ => await BuildSummaryAsync(userId, flow, ct)
        };

        if (messageId.HasValue)
        {
            await bot.EditMessageTextAsync(chatId, messageId.Value, text,
                ParseMode.Markdown, replyMarkup: keyboard, cancellationToken: ct);
        }
        else
        {
            await bot.SendTextMessageAsync(chatId, text,
                ParseMode.Markdown, replyMarkup: keyboard, cancellationToken: ct);
        }
    }

    // Получить период (от — до) и лейбл
    private (DateTimeOffset from, DateTimeOffset to, string label) GetPeriodRange(UserFlowState flow)
    {
        var date = flow.StatsDate;
        return flow.StatsPeriod switch
        {
            StatsPeriod.Week => (
                date.AddDays(-(int)date.DayOfWeek + 1),
                date.AddDays(7 - (int)date.DayOfWeek),
                $"Неделя {date:dd.MM}"
            ),
            StatsPeriod.Month => (
                new DateTimeOffset(date.Year, date.Month, 1, 0, 0, 0, date.Offset),
                new DateTimeOffset(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month), 23, 59, 59, date.Offset),
                date.ToString("MMMM yyyy", new System.Globalization.CultureInfo("ru-RU"))
            ),
            StatsPeriod.Year => (
                new DateTimeOffset(date.Year, 1, 1, 0, 0, 0, date.Offset),
                new DateTimeOffset(date.Year, 12, 31, 23, 59, 59, date.Offset),
                date.Year.ToString()
            ),
            _ => (date.AddDays(-30), date, "Период")
        };
    }

    // ===== ЭКРАН 1: СВОДКА =====
    private async Task<(string, Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup)> BuildSummaryAsync(
        long userId, UserFlowState flow, CancellationToken ct)
    {
        var (from, to, label) = GetPeriodRange(flow);

        // Получаем данные
        var totalIncome = await transactionService.GetTotalIncomeAsync(userId, from, ct);
        var totalExpense = await transactionService.GetTotalExpenseAsync(userId, from, ct);
        var balance = totalIncome - totalExpense;

        // Топ расходов
        var topExpenses = await transactionService.GetTopExpensesAsync(userId, from, 3, ct);

        // Эффективность
        decimal savingsPercent = totalIncome > 0 ? (balance / totalIncome) * 100 : 0;
        
        // Эмоции
        var emotions = await transactionService.GetExpensesByPeriodAsync(userId, from, to, ct);
        var emotionalSum = emotions.Where(t => t.IsImpulsive).Sum(t => t.Amount);
        decimal emotionsPercent = totalExpense > 0 ? (emotionalSum / totalExpense) * 100 : 0;

        // Регулярные
        var regulars = await regularPaymentService.GetActiveAsync(userId, ct);
        var regularSum = regulars.Sum(r => r.Amount);
        decimal regularPercent = totalIncome > 0 ? (regularSum / totalIncome) * 100 : 0;

        // Собираем текст
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"📊 *Статистика: {label}*");
        sb.AppendLine();
        sb.AppendLine($"💰 *ИТОГ:* `{(balance >= 0 ? "+" : "")}{balance:F0} TJS`");
        sb.AppendLine($"(📥 {totalIncome:F0} — 📤 {totalExpense:F0})");
        sb.AppendLine();
        sb.AppendLine("🏆 *Топ Расходов:*");

        int i = 1;
        foreach (var (cat, amount) in topExpenses)
        {
            var percent = totalExpense > 0 ? (amount / totalExpense) * 100 : 0;
            var limitInfo = "";
            var limit = await limitService.GetByCategoryAsync(userId, cat.Id, ct);
            if (limit != null)
            {
                var limitPercent = (amount / limit.Amount) * 100;
                limitInfo = limitPercent > 100 ? " ⚠️" : "";
            }
            sb.AppendLine($"{i}. {cat.Icon ?? "📁"} {cat.Name}: *{amount:F0}* ({percent:F0}%){limitInfo}");
            i++;
        }

        sb.AppendLine();
        sb.AppendLine("📈 *Эффективность:*");
        sb.AppendLine($"🟢 Сбережения: *{savingsPercent:F0}%*");
        sb.AppendLine($"🟡 Обязательные: *{regularPercent:F0}%*");
        var emotionColor = emotionsPercent > 30 ? "🔴" : "🟡";
        sb.AppendLine($"{emotionColor} На эмоциях: *{emotionsPercent:F0}%*");
        sb.AppendLine();
        
        // Прогноз
        var daysPassed = Math.Max(1, (DateTimeOffset.UtcNow - from).Days);
        var avgDaily = totalExpense / daysPassed;
        var daysLeft = Math.Max(0, (to - DateTimeOffset.UtcNow).Days);
        var projectedExpense = totalExpense + (avgDaily * daysLeft);
        var projectedBalance = totalIncome - projectedExpense;
        sb.AppendLine($"🔮 *Прогноз:* К концу периода: *{(projectedBalance >= 0 ? "+" : "")}{projectedBalance:F0} TJS*");

        return (sb.ToString(), BotInlineKeyboards.StatsSummary(label));
    }

    // ===== ЭКРАН 2: КАТЕГОРИИ =====
    private async Task<(string, Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup)> BuildCategoriesAsync(
        long userId, UserFlowState flow, CancellationToken ct)
    {
        var (from, to, label) = GetPeriodRange(flow);

        IReadOnlyList<Domain.Entities.Transaction> transactions;
        if (flow.StatsShowExpenses)
        {
            transactions = await transactionService.GetExpensesByPeriodAsync(userId, from, to, ct);
        }
        else
        {
            var allTxn = await transactionService.GetUserTransactionsAsync(userId, 500, ct);
            transactions = allTxn.Where(t => t.Type == TransactionType.Income && t.Date >= from && t.Date <= to).ToList();
        }

        var grouped = transactions
            .Where(t => t.Category != null)
            .GroupBy(t => t.Category!)
            .Select(g => new { Category = g.Key, Total = g.Sum(t => t.Amount), Count = g.Count() })
            .OrderByDescending(x => x.Total)
            .Take(10)
            .ToList();

        var total = grouped.Sum(g => g.Total);

        var sb = new System.Text.StringBuilder();
        var typeLabel = flow.StatsShowExpenses ? "📤 РАСХОДЫ" : "📥 ДОХОДЫ";
        sb.AppendLine($"📂 *Категории: {label}*");
        sb.AppendLine();
        sb.AppendLine($"*Вид:* {typeLabel} (Всего: {total:F0})");
        sb.AppendLine();

        int i = 1;
        foreach (var g in grouped)
        {
            var pct = total > 0 ? (g.Total / total) * 100 : 0;
            var limitInfo = "";
            if (flow.StatsShowExpenses)
            {
                var limit = await limitService.GetByCategoryAsync(userId, g.Category.Id, ct);
                if (limit != null)
                {
                    var limitPct = (g.Total / limit.Amount) * 100;
                    limitInfo = limitPct > 100 ? $" ⚠️ {limit.Amount:F0}" : $" / {limit.Amount:F0}";
                }
            }
            sb.AppendLine($"{i}. {g.Category.Icon ?? "📁"} *{g.Category.Name}*");
            sb.AppendLine($"   — {g.Total:F0} TJS ({pct:F0}%) • {g.Count} опер.{limitInfo}");
            i++;
        }

        return (sb.ToString(), BotInlineKeyboards.StatsCategories(flow.StatsShowExpenses));
    }

    // ===== ЭКРАН 3: ИСТОРИЯ =====
    private async Task<(string, Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup)> BuildHistoryAsync(
        long userId, UserFlowState flow, CancellationToken ct)
    {
        var (from, to, label) = GetPeriodRange(flow);
        const int pageSize = 10;

        var (items, totalCount) = await transactionService.GetTransactionsPageAsync(
            userId, flow.StatsPage, pageSize, null, from, null, ct);

        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
        if (totalPages == 0) totalPages = 1;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"📜 *История: {label}*");
        sb.AppendLine($"*Страница {flow.StatsPage} из {totalPages}*");
        sb.AppendLine();

        string? lastDate = null;
        foreach (var txn in items)
        {
            var dateStr = txn.Date.ToString("dd.MM.yyyy");
            if (dateStr != lastDate)
            {
                sb.AppendLine($"*{dateStr}*");
                lastDate = dateStr;
            }
            var sign = txn.Type == TransactionType.Income ? "+" : "-";
            var icon = txn.Category?.Icon ?? "📝";
            var desc = txn.Description ?? txn.Category?.Name ?? "";
            var impulsive = txn.IsImpulsive ? " 🌪" : "";
            sb.AppendLine($"{icon} {desc}: `{sign}{txn.Amount:F0}`{impulsive}");
        }

        sb.AppendLine();
        sb.AppendLine($"_Всего операций: {totalCount}_");

        return (sb.ToString(), BotInlineKeyboards.StatsHistory(flow.StatsPage, totalPages));
    }

    // ===== ЭКРАН 4: ЭМОЦИИ =====
    private async Task<(string, Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup)> BuildEmotionsAsync(
        long userId, UserFlowState flow, CancellationToken ct)
    {
        var (from, to, label) = GetPeriodRange(flow);

        var expenses = await transactionService.GetExpensesByPeriodAsync(userId, from, to, ct);
        var emotional = expenses.Where(t => t.IsImpulsive).OrderByDescending(t => t.Amount).Take(5).ToList();
        var emotionalSum = emotional.Sum(t => t.Amount);
        var totalExpense = expenses.Sum(t => t.Amount);
        var percent = totalExpense > 0 ? (emotionalSum / totalExpense) * 100 : 0;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"🌪 *Эмоциональные Покупки: {label}*");
        sb.AppendLine();
        sb.AppendLine($"💸 *Всего:* `{emotionalSum:F0} TJS`");
        sb.AppendLine($"({percent:F0}% от всех расходов)");
        sb.AppendLine();
        sb.AppendLine("🏆 *Топ-5 \"Грехов\":*");

        int i = 1;
        foreach (var txn in emotional)
        {
            var desc = txn.Description ?? txn.Category?.Name ?? "Покупка";
            sb.AppendLine($"{i}. {txn.Category?.Icon ?? "🛒"} {desc}: *{txn.Amount:F0}* ({txn.Date:dd.MM})");
            i++;
        }

        if (!emotional.Any())
        {
            sb.AppendLine("_Нет эмоциональных трат! Вы — финансовый ниндзя!_ 🥷");
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine("🚀 *Мотивация:*");
            sb.AppendLine($"_Без этих трат вы бы сэкономили {emotionalSum:F0} TJS!_");
        }

        return (sb.ToString(), BotInlineKeyboards.StatsEmotions());
    }

    // ===== ЭКРАН 5: РЕГУЛЯРНЫЕ =====
    private async Task<(string, Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup)> BuildRegularAsync(
        long userId, UserFlowState flow, CancellationToken ct)
    {
        var regulars = await regularPaymentService.GetActiveAsync(userId, ct);
        var totalMonthly = regulars.Sum(r => r.Amount);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("📅 *Регулярные Платежи*");
        sb.AppendLine();
        sb.AppendLine($"💳 *Нагрузка:* `{totalMonthly:F0} TJS/мес`");
        sb.AppendLine();

        if (!regulars.Any())
        {
            sb.AppendLine("_Нет регулярных платежей._");
        }
        else
        {
            sb.AppendLine("📋 *Список:*");
            foreach (var r in regulars.Take(10))
            {
                var freqLabel = r.Frequency switch
                {
                    PaymentFrequency.Daily => "ежедневно",
                    PaymentFrequency.Weekly => "еженедельно",
                    PaymentFrequency.Monthly => "ежемесячно",
                    PaymentFrequency.Yearly => "ежегодно",
                    _ => ""
                };
                var next = r.NextDueDate?.ToString("dd.MM") ?? "—";
                sb.AppendLine($"• {r.Name}: *{r.Amount:F0}* ({freqLabel}) — след. {next}");
            }
        }

        return (sb.ToString(), BotInlineKeyboards.StatsRegular());
    }

    // ===== ЭКРАН 6: ВЫБОР ПЕРИОДА =====
    private (string, Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup) BuildPeriodSelect(UserFlowState flow)
    {
        var currentLabel = flow.StatsPeriod switch
        {
            StatsPeriod.Week => "Неделя",
            StatsPeriod.Month => "Месяц",
            StatsPeriod.Year => "Год",
            _ => "Месяц"
        };

        var text = $"📅 *Выберите тип периода*\n\nТекущий: *{currentLabel}*";
        return (text, BotInlineKeyboards.StatsPeriodSelect());
    }
}
