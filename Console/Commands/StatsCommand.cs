using Console.Bot;
using Console.Flow;
using Domain.Enums;
using Infrastructure.Services;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace Console.Commands;

// Хаб статистики с несколькими экранами
public class StatsCommand(
    ITransactionService transactionService,
    ILimitService limitService,
    IRegularPaymentService regularPaymentService,
    IAccountService accountService)
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
        System.Console.WriteLine($"[StatsCommand] Rendering screen: {flow.CurrentStatsScreen}");
        try 
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

            System.Console.WriteLine($"[StatsCommand] Built text (len={text.Length}). Editing message...");

            if (messageId.HasValue)
            {
                await bot.EditMessageTextAsync(chatId, messageId.Value, text,
                    ParseMode.Markdown, replyMarkup: keyboard, cancellationToken: ct);
                System.Console.WriteLine("[StatsCommand] Edit success.");
            }
            else
            {
                await bot.SendTextMessageAsync(chatId, text,
                    ParseMode.Markdown, replyMarkup: keyboard, cancellationToken: ct);
                System.Console.WriteLine("[StatsCommand] Send success.");
            }
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"[StatsCommand] ERROR: {ex.Message}");
            System.Console.WriteLine(ex.StackTrace);
            await bot.SendTextMessageAsync(chatId, $"Ошибка отображения: {ex.Message}", cancellationToken: ct);
        }
    }

    // Получить дату регистрации
    private async Task<DateTimeOffset> GetRegDateAsync(long userId, CancellationToken ct)
    {
         var account = await accountService.GetUserAccountAsync(userId, ct);
         return account?.CreatedAt ?? DateTimeOffset.UtcNow;
    }

    // Получить период (от — до) и лейбл
    private (DateTimeOffset from, DateTimeOffset to, string label) GetPeriodRange(UserFlowState flow, DateTimeOffset regDate)
    {
        var date = flow.StatsDate;
        var culture = new System.Globalization.CultureInfo("ru-RU");

        return flow.StatsPeriod switch
        {
            StatsPeriod.Week => CalculateCustomWeek(date, regDate),
            StatsPeriod.Month => (
                new DateTimeOffset(date.Year, date.Month, 1, 0, 0, 0, date.Offset),
                new DateTimeOffset(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month), 23, 59, 59, date.Offset),
                culture.TextInfo.ToTitleCase(date.ToString("MMMM", culture)) // Первая буква заглавная, без года
            ),
            StatsPeriod.Year => (
                new DateTimeOffset(date.Year, 1, 1, 0, 0, 0, date.Offset),
                new DateTimeOffset(date.Year, 12, 31, 23, 59, 59, date.Offset),
                date.Year.ToString()
            ),
             StatsPeriod.AllTime => (
                DateTimeOffset.MinValue,
                DateTimeOffset.MaxValue,
                "За все время"
            ),
            _ => (date.AddDays(-30), date, "Период")
        };
    }

    private (DateTimeOffset from, DateTimeOffset to, string label) CalculateCustomWeek(DateTimeOffset date, DateTimeOffset regDate)
    {
        // Неделя считается блоками по 7 дней ОТ даты регистрации.
        // Нужно найти начало текущего 7-дневного блока, в который попадает date.
        
        // Нормализуем время регистрации в начало дня для корректного счета
        var baseDate = new DateTimeOffset(regDate.Year, regDate.Month, regDate.Day, 0,0,0, regDate.Offset);
        
        // Сколько дней прошло от регистрации до текущей даты просмотра
        var diffDays = (date - baseDate).TotalDays;
        
        // Индекс недели (0 - первая неделя, 1 - вторая...)
        var weekIndex = (int)Math.Floor(diffDays / 7.0);
        if (weekIndex < 0) weekIndex = 0; // Не уходим в минус

        var start = baseDate.AddDays(weekIndex * 7);
        var end = start.AddDays(6).AddHours(23).AddMinutes(59).AddSeconds(59); // Конец 7-го дня

        return (start, end, $"{start:dd.MM} - {end:dd.MM}");
    }

    // ===== ЭКРАН 1: СВОДКА =====
    private async Task<(string, Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup)> BuildSummaryAsync(
        long userId, UserFlowState flow, CancellationToken ct)
    {
        var regDate = await GetRegDateAsync(userId, ct);
        var (from, to, label) = GetPeriodRange(flow, regDate);

        // Получаем данные
        var totalIncome = await transactionService.GetTotalIncomeAsync(userId, from, ct);
        var totalExpense = await transactionService.GetTotalExpenseAsync(userId, from, ct);
        var balance = totalIncome - totalExpense;

        // Топ расходов
        var topExpenses = await transactionService.GetTopExpensesAsync(userId, from, 3, ct);
        // Топ доходов (добавлено по требованию)
        var topIncomes = await transactionService.GetTopIncomesAsync(userId, from, 3, ct);

        // Собираем текст
        // Формируем заголовок для сводки
        var headerLabel = label;
        if (flow.StatsPeriod == StatsPeriod.Month) headerLabel = $"{label} {flow.StatsDate.Year}";
        if (flow.StatsPeriod == StatsPeriod.Week) headerLabel = $"Неделя {label}"; // "Неделя 12.01 - 18.01"

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"📊 *Статистика: {headerLabel}*");
        
        // Примечание для текущей неполной недели
        if (flow.StatsPeriod == StatsPeriod.Week && to > DateTimeOffset.UtcNow)
        {
             var now = DateTimeOffset.UtcNow;
             sb.AppendLine($"_(текущая неделя: учтены дни с {from:dd.MM} по {now:dd} числа)_");
        }
        
        sb.AppendLine();
        
        sb.AppendLine($"📥 Доход: *+{totalIncome:N0} TJS*");
        sb.AppendLine($"📤 Расход: *-{totalExpense:N0} TJS*");
        var resultSign = balance >= 0 ? "+" : "";
        sb.AppendLine($"💰 Итог: *{resultSign}{balance:N0} TJS*");
        sb.AppendLine();

        if (topIncomes.Any())
        {
            sb.AppendLine("🏆 *Топ Доходов:*");
            int j = 1;
            foreach (var (cat, amount) in topIncomes)
            {
                var percent = totalIncome > 0 ? (amount / totalIncome) * 100 : 0;
                sb.AppendLine($"{j}. {cat.Name}: *+{amount:N0}* ({percent:F0}%)");
                j++;
            }
            sb.AppendLine();
        }

        if (topExpenses.Any())
        {
            sb.AppendLine("📈 *Топ Расходов:*");
            int i = 1;
            foreach (var (cat, amount) in topExpenses)
            {
                var percent = totalExpense > 0 ? (amount / totalExpense) * 100 : 0;
                sb.AppendLine($"{i}. {cat.Name}: *-{amount:N0}* ({percent:F0}%)");
                i++;
            }
            sb.AppendLine();
        }
        
        // Прогноз (только для текущего периода)
        var daysPassed = Math.Max(1, (DateTimeOffset.UtcNow - from).Days);
        if (to > DateTimeOffset.UtcNow && daysPassed > 0 && daysPassed < 30) // Показываем прогноз только если есть смысл и период еще не прошел
        {
            var avgDaily = totalExpense / daysPassed;
            var daysLeft = Math.Max(0, (to - DateTimeOffset.UtcNow).Days);
            var projectedExpense = totalExpense + (avgDaily * daysLeft);
            var projectedBalance = totalIncome - projectedExpense;
            sb.AppendLine($"🔮 *Прогноз:* Остаток к концу: *{(projectedBalance >= 0 ? "+" : "")}{projectedBalance:N0} TJS*");
        }

        // Логика кнопок
        bool canGoBack = true;
        bool canGoForward = true;

        if (flow.StatsPeriod == StatsPeriod.AllTime)
        {
            canGoBack = false;
            canGoForward = false;
        }
        else
        {
            // Назад: если текущий период начинается раньше или одновременно с датой регистрации -> нельзя назад
            // Для недели: если start <= regDate -> нельзя.
            // Для месяца: если месяц <= месяца регистрации -> нельзя.
            
            // Получаем "предыдущий" период теоретически
            // Но проще проверить начало текущего.
            // Если начало текущего периода МЕНЬШЕ ИЛИ РАВНО (с точностью до дня) дате регистрации -> значит мы на первом периоде.
            
            // Нюанс: regDate может быть 12:00, from 00:00.
            if (from.Date <= regDate.Date) canGoBack = false;
            
            // Если месяц/год:
            if (flow.StatsPeriod == StatsPeriod.Month && (from.Year < regDate.Year || (from.Year == regDate.Year && from.Month <= regDate.Month))) canGoBack = false;
            if (flow.StatsPeriod == StatsPeriod.Year && from.Year <= regDate.Year) canGoBack = false;

            // Вперед: если конец текущего периода >= текущего времени -> нельзя вперед
            if (to >= DateTimeOffset.UtcNow) canGoForward = false;
        }

        // Лейбл для кнопки периода (статичный)
        var btnLabel = flow.StatsPeriod switch
        {
            StatsPeriod.Week => "Неделя",
            StatsPeriod.Month => label, // Январь
            StatsPeriod.Year => label, // 2024
            StatsPeriod.AllTime => "За все время",
            _ => "Период"
        };

        return (sb.ToString(), BotInlineKeyboards.StatsSummary(btnLabel, canGoBack, canGoForward));
    }

    // ===== ЭКРАН 2: КАТЕГОРИИ =====
    private async Task<(string, Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup)> BuildCategoriesAsync(
        long userId, UserFlowState flow, CancellationToken ct)
    {
        var regDate = await GetRegDateAsync(userId, ct);
        var (from, to, label) = GetPeriodRange(flow, regDate);

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
        var typeLabel = flow.StatsShowExpenses ? "Расходы по категориям" : "Доходы по категориям";
        
        sb.AppendLine($"📂 *{typeLabel}: {label}*");
        sb.AppendLine();
        sb.AppendLine($"💰 *Всего за период:* {total:N0} TJS");
        sb.AppendLine();
        sb.AppendLine($"📉 *Топ категорий:*");

        // 1. Подготовка данных для выравнивания
        var formattedItems = grouped.Select(g => new 
        { 
            AmountStr = $"{g.Total:N0}", 
            Icon = g.Category.Icon ?? "📁", 
            Name = g.Category.Name, 
            Count = g.Count 
        }).ToList();

        var maxLen = formattedItems.Any() ? formattedItems.Max(x => x.AmountStr.Length) : 0;

        foreach (var item in formattedItems)
        {
            var paddedAmount = item.AmountStr.PadRight(maxLen + 1);
            // Формат: *Amount* Icon Category (Count)
            sb.AppendLine($"*{paddedAmount}* {item.Icon} {item.Name} ({item.Count} оп.)");
        }

        return (sb.ToString(), BotInlineKeyboards.StatsCategories(flow.StatsShowExpenses));
    }

    // ===== ЭКРАН 3: ИСТОРИЯ =====
    private async Task<(string, Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup)> BuildHistoryAsync(
        long userId, UserFlowState flow, CancellationToken ct)
    {
        var regDate = await GetRegDateAsync(userId, ct);
        var (from, to, label) = GetPeriodRange(flow, regDate);
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
        
        // 1. Подготовка данных для выравнивания
        var formattedItems = items.Select(txn => {
            var sign = txn.Type == TransactionType.Income ? "+" : ""; // Минус уже есть в самой сумме если она отрицательная? Нет, TransactionService возвращает absolute amount или signed?
            // Обычно transaction amount положительный.
            var signChar = txn.Type == TransactionType.Income ? "+" : "-";
            var amountStr = $"{signChar}{txn.Amount:F0}"; // -12 или +100
            
            var icon = txn.Category?.Icon ?? "📝";
            var desc = txn.Description ?? txn.Category?.Name ?? "";
            var impulsive = txn.IsImpulsive ? " 🌪" : "";
            var dateStr = txn.Date.ToString("dd.MM.yyyy");
            
            return new { AmountStr = amountStr, Icon = icon, Desc = desc, Impulsive = impulsive, DateStr = dateStr };
        }).ToList();

        // Находим макс длину суммы для выравнивания
        var maxLen = formattedItems.Any() ? formattedItems.Max(x => x.AmountStr.Length) : 0;

        foreach (var item in formattedItems)
        {
            if (item.DateStr != lastDate)
            {
                sb.AppendLine($"*{item.DateStr}*");
                lastDate = item.DateStr;
            }
            
            // Выравнивание: добавляем пробелы
            // Формат: `Amount   ` Icon Desc
            // Добавляем 1-2 пробела как просили "пробель после суммы" (имеется ввиду отступ до текста)
            var paddedAmount = item.AmountStr.PadRight(maxLen + 1); // +1 для минимума 1 пробела
            // Чтобы `PadRight` работал визуально в Telegram Markdown, лучше использовать `Insert` пробелов внутрь `code` блока или просто пробелы?
            // В Telegram моноширинный шрифт только внутри `...`. 
            // Пробуем: `amount`   text
            
            sb.AppendLine($"*{paddedAmount}* {item.Icon} {item.Desc}{item.Impulsive}");
        }

        sb.AppendLine();
        sb.AppendLine($"_Всего операций: {totalCount}_");

        return (sb.ToString(), BotInlineKeyboards.StatsHistory(flow.StatsPage, totalPages));
    }

    // ===== ЭКРАН 4: ЭМОЦИИ =====
    private async Task<(string, Telegram.Bot.Types.ReplyMarkups.InlineKeyboardMarkup)> BuildEmotionsAsync(
        long userId, UserFlowState flow, CancellationToken ct)
    {
        var regDate = await GetRegDateAsync(userId, ct);
        var (from, to, label) = GetPeriodRange(flow, regDate);

        var expenses = await transactionService.GetExpensesByPeriodAsync(userId, from, to, ct);
        var emotional = expenses.Where(t => t.IsImpulsive).OrderByDescending(t => t.Amount).Take(5).ToList();
        var emotionalSum = emotional.Sum(t => t.Amount);
        var totalExpense = expenses.Sum(t => t.Amount);
        var percent = totalExpense > 0 ? (emotionalSum / totalExpense) * 100 : 0;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"🌪 *Эмоциональные Покупки за {label}*");
        sb.AppendLine();
        sb.AppendLine($"💸 *Всего:* `{emotionalSum:F0} TJS`");
        sb.AppendLine($"({percent:F0}% от всех расходов)");
        sb.AppendLine();
        sb.AppendLine("📈 *Топ-5 Эмоциональных покупок:*");

        int i = 1;
        foreach (var txn in emotional)
        {
            var desc = txn.Description ?? txn.Category?.Name ?? "Покупка";
            sb.AppendLine($"{i}. {txn.Category?.Icon ?? "🛒"} {desc}: *{txn.Amount:N0}* ({txn.Date:dd.MM})");
            i++;
        }

        if (!emotional.Any())
        {
            sb.AppendLine("_Нет эмоциональных трат! Вы — финансовый ниндзя!_ 🥷");
        }
        else
        {
            sb.AppendLine();
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
        sb.AppendLine($"💳 В месяц по *{totalMonthly:F0} TJS*");
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
            StatsPeriod.AllTime => "За все время",
            _ => "Месяц"
        };

        var text = $"📅 *Выберите отчетный период*\n\n" +
                   $"Здесь вы можете переключить режим отображения статистики.\n" +
                   $"Текущий режим: *{currentLabel}*";
        
        return (text, BotInlineKeyboards.StatsPeriodSelect());
    }
}
