using Telegram.Bot.Types.ReplyMarkups;

namespace Console.Bot.Keyboards;

// Клавиатуры для модуля Целей (v3 — полные сценарии)
public static class GoalKeyboards
{
    // Главная карточка цели
    public static InlineKeyboardMarkup MainKeyboard() => new(new[]
    {
        new[] 
        { 
            InlineKeyboardButton.WithCallbackData("📋 Все цели", "goal:list"),
            InlineKeyboardButton.WithCallbackData("➕ Пополнить", "goal:deposit")
        },
        new[] 
        { 
            InlineKeyboardButton.WithCallbackData("⚙️ Настройки", "goal:settings"),
            InlineKeyboardButton.WithCallbackData("🔙 Назад", "menu:main")
        }
    });

    // После создания цели
    public static InlineKeyboardMarkup AfterCreate(int goalId, bool isFirst) => new(isFirst
        ? new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("✅ Готово", "goal:main") }
        }
        : new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("⭐ Сделать главной", $"goal:setmain:{goalId}") },
            new[] { InlineKeyboardButton.WithCallbackData("✅ Готово", "goal:list") }
        });

    // Экран пополнения
    public static InlineKeyboardMarkup Deposit(decimal suggestedAmount)
    {
        var buttons = new List<InlineKeyboardButton[]>();
        if (suggestedAmount > 0)
            buttons.Add(new[] { InlineKeyboardButton.WithCallbackData($"💰 {suggestedAmount:N0} TJS", $"goal:add:{suggestedAmount}") });
        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("🔙 Назад", "goal:main") });
        return new InlineKeyboardMarkup(buttons);
    }

    // Настройки цели
    public static InlineKeyboardMarkup Settings(int goalId) => new(new[]
    {
        new[]
        {
            InlineKeyboardButton.WithCallbackData("✏️ Название", $"goal:edit:name:{goalId}"),
            InlineKeyboardButton.WithCallbackData("💵 Сумма", $"goal:edit:amount:{goalId}")
        },
        new[] { InlineKeyboardButton.WithCallbackData("📅 Дедлайн", $"goal:edit:deadline:{goalId}") },
        new[] 
        { 
            InlineKeyboardButton.WithCallbackData("➖ Взять", "goal:withdraw"),
            InlineKeyboardButton.WithCallbackData("🗑 Удалить", $"goal:delete:{goalId}") 
        },
        new[] { InlineKeyboardButton.WithCallbackData("🔙 Назад", "goal:main") }
    });

    // Экран снятия
    public static InlineKeyboardMarkup Withdraw(decimal suggestedAmount)
    {
        var buttons = new List<InlineKeyboardButton[]>();
        if (suggestedAmount > 0)
            buttons.Add(new[] { InlineKeyboardButton.WithCallbackData($"💸 {suggestedAmount:N0} TJS", $"goal:take:{suggestedAmount}") });
        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("🔙 Назад", "goal:settings") });
        return new InlineKeyboardMarkup(buttons);
    }

    // Список целей с пагинацией
    public static InlineKeyboardMarkup List(int page, int totalPages)
    {
        var buttons = new List<InlineKeyboardButton[]>();
        
        // Ряд пагинации: <  стр/всего  >
        var navRow = new List<InlineKeyboardButton>();
        
        // Кнопка Назад
        if (page > 0)
            navRow.Add(InlineKeyboardButton.WithCallbackData("⬅️", $"goal:list:{page - 1}"));
        else
            navRow.Add(InlineKeyboardButton.WithCallbackData(" ", "goal:noop"));
            
        // Счетчик
        navRow.Add(InlineKeyboardButton.WithCallbackData($"{page + 1}/{totalPages}", "goal:noop"));
        
        // Кнопка Вперед
        if (page < totalPages - 1)
            navRow.Add(InlineKeyboardButton.WithCallbackData("➡️", $"goal:list:{page + 1}"));
        else
            navRow.Add(InlineKeyboardButton.WithCallbackData(" ", "goal:noop"));

        buttons.Add(navRow.ToArray());

        // Управление
        buttons.Add(new[] 
        { 
            InlineKeyboardButton.WithCallbackData("➕ Новая цель", "goal:create"), 
            InlineKeyboardButton.WithCallbackData("🔙 Назад", "goal:main") 
        });
        
        return new InlineKeyboardMarkup(buttons);
    }

    // Победа! (цель достигнута)
    public static InlineKeyboardMarkup Victory(int goalId) => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("🛍 Купил! (списать)", $"goal:bought:{goalId}") },
        new[] { InlineKeyboardButton.WithCallbackData("📈 Копить дальше", $"goal:continue:{goalId}") },
        new[] { InlineKeyboardButton.WithCallbackData("🔙 В меню целей", "goal:main") }
    });

    // Победа с переполнением (есть остаток денег)
    public static InlineKeyboardMarkup VictoryWithOverflow(int goalId, decimal excess) => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("🎯 Выбрать другую цель", $"goal:overflow:{excess}") },
        new[] { InlineKeyboardButton.WithCallbackData($"💰 Оставить {excess:N0} на балансе", $"goal:overflow:keep:{goalId}") },
        new[] { InlineKeyboardButton.WithCallbackData("🔙 В меню целей", "goal:main") }
    });

    // Выбор цели для переполнения
    public static InlineKeyboardMarkup OverflowTargets(IReadOnlyList<Domain.Entities.Goal> goals, decimal amount) => new(
        goals.Take(5).Select(g => new[] { InlineKeyboardButton.WithCallbackData($"🎯 {g.Name}", $"goal:overflow:to:{g.Id}:{amount}") })
        .Append(new[] { InlineKeyboardButton.WithCallbackData("💰 Оставить на балансе", "goal:main") })
        .Append(new[] { InlineKeyboardButton.WithCallbackData("🔙 Назад", "goal:main") })
        .ToArray());

    // После покупки (показать следующую цель)
    public static InlineKeyboardMarkup AfterBought(bool hasNextGoal) => new(hasNextGoal
        ? new[]
        {
            new[] 
            { 
                InlineKeyboardButton.WithCallbackData("📋 Все цели", "goal:list"),
                InlineKeyboardButton.WithCallbackData("➕ Пополнить", "goal:deposit")
            },
            new[] 
            { 
                InlineKeyboardButton.WithCallbackData("⚙️ Настройки", "goal:settings"),
                InlineKeyboardButton.WithCallbackData("🔙 Назад", "menu:main")
            }
        }
        : new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("➕ Создать новую цель", "goal:create") },
            new[] { InlineKeyboardButton.WithCallbackData("🔙 В главное меню", "menu:main") }
        });

    // Подтверждение удаления
    public static InlineKeyboardMarkup DeleteConfirm(int goalId) => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("🗑 Да, удалить", $"goal:delete:confirm:{goalId}") },
        new[] { InlineKeyboardButton.WithCallbackData("🔙 Отмена", "goal:settings") }
    });

    // Нет целей
    public static InlineKeyboardMarkup Empty() => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("➕ Создать первую цель", "goal:create") },
        new[] { InlineKeyboardButton.WithCallbackData("🔙 Назад", "menu:main") }
    });

    // Все цели завершены
    public static InlineKeyboardMarkup AllCompleted() => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("➕ Создать новую цель", "goal:create") },
        new[] { InlineKeyboardButton.WithCallbackData("🔙 В главное меню", "menu:main") }
    });

    // Отмена
    public static InlineKeyboardMarkup Cancel(string callbackData = "goal:main") => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("🔙 Отмена", callbackData) }
    });

    // Умный расчёт суммы для пополнения с учётом цели
    public static decimal CalculateSmartDeposit(decimal balance, decimal remaining)
    {
        if (balance <= 0 || remaining <= 0) return 0;
        var maxDeposit = Math.Min(balance, remaining);
        
        decimal unit;
        if (maxDeposit >= 10000) unit = 10000;
        else if (maxDeposit >= 1000) unit = 1000;
        else if (maxDeposit >= 100) unit = 100;
        else unit = 10;
        
        var rounded = Math.Floor(maxDeposit / unit) * unit;
        var remainder = maxDeposit - rounded;
        return remainder > 0 ? remainder : maxDeposit;
    }
}
