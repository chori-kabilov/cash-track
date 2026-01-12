using Telegram.Bot.Types.ReplyMarkups;

namespace Console.Bot.Keyboards;

// Клавиатуры для регулярных платежей
public static class RegularKeyboards
{
    // Дашборд
    public static InlineKeyboardMarkup Dashboard() => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("📋 Все платежи", "regular:list") },
        new[] 
        { 
            InlineKeyboardButton.WithCallbackData("➕ Добавить", "regular:create"),
            InlineKeyboardButton.WithCallbackData("🔙 Меню", "menu:main")
        }
    });

    // Пустой экран
    public static InlineKeyboardMarkup Empty() => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("➕ Добавить платёж", "regular:create") },
        new[] { InlineKeyboardButton.WithCallbackData("🔙 Главное меню", "menu:main") }
    });

    // Отмена
    public static InlineKeyboardMarkup Cancel() => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("❌ Отмена", "regular:main") }
    });

    // Выбор периодичности
    public static InlineKeyboardMarkup Frequency() => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("📅 Еженедельно", "regular:freq:weekly") },
        new[] { InlineKeyboardButton.WithCallbackData("📅 Ежемесячно", "regular:freq:monthly") },
        new[] { InlineKeyboardButton.WithCallbackData("📅 Ежегодно", "regular:freq:yearly") },
        new[] { InlineKeyboardButton.WithCallbackData("❌ Отмена", "regular:main") }
    });

    // Тип платежа
    public static InlineKeyboardMarkup PaymentType() => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("🅰️ Фиксированный", "regular:type:fixed") },
        new[] { InlineKeyboardButton.WithCallbackData("🅱️ Плавающий", "regular:type:floating") },
        new[] { InlineKeyboardButton.WithCallbackData("❌ Отмена", "regular:main") }
    });

    // Выбор дня
    public static InlineKeyboardMarkup DayOfMonth() => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("📅 Последний день", "regular:day:last") },
        new[] { InlineKeyboardButton.WithCallbackData("❌ Отмена", "regular:main") }
    });

    // Без категории


    // Список с пагинацией
    public static InlineKeyboardMarkup List(int page, int totalPages)
    {
        var buttons = new List<InlineKeyboardButton[]>();

        // Пагинация
        var navRow = new List<InlineKeyboardButton>();
        if (page > 0)
            navRow.Add(InlineKeyboardButton.WithCallbackData("⬅️", $"regular:list:{page - 1}"));
        else
            navRow.Add(InlineKeyboardButton.WithCallbackData(" ", "regular:noop"));
        navRow.Add(InlineKeyboardButton.WithCallbackData($"{page + 1}/{totalPages}", "regular:noop"));
        if (page < totalPages - 1)
            navRow.Add(InlineKeyboardButton.WithCallbackData("➡️", $"regular:list:{page + 1}"));
        else
            navRow.Add(InlineKeyboardButton.WithCallbackData(" ", "regular:noop"));
        buttons.Add(navRow.ToArray());

        // Управление
        buttons.Add(new[] 
        { 
            InlineKeyboardButton.WithCallbackData("➕ Добавить", "regular:create"),
            InlineKeyboardButton.WithCallbackData("🔙 Назад", "regular:main")
        });
        return new InlineKeyboardMarkup(buttons);
    }

    // Детали платежа
    public static InlineKeyboardMarkup Detail(int paymentId, bool isPaused, bool hasEnoughBalance)
    {
        var buttons = new List<InlineKeyboardButton[]>();

        // Кнопка Оплатить (с предупреждением если не хватает денег)
        var payLabel = hasEnoughBalance ? "✅ Оплачено" : "⚠️ Оплачено (недостаточно)";
        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData(payLabel, $"regular:pay:{paymentId}") });

        // История и редактирование
        buttons.Add(new[] 
        { 
            InlineKeyboardButton.WithCallbackData("📜 История", $"regular:history:{paymentId}"), 
            InlineKeyboardButton.WithCallbackData("✏️ Изменить", $"regular:edit:{paymentId}") 
        });

        // Пауза/Возобновление
        var pauseLabel = isPaused ? "▶️ Возобновить" : "⏸ Остановить";
        var pauseAction = isPaused ? "resume" : "pause";
        buttons.Add(new[] 
        { 
            InlineKeyboardButton.WithCallbackData(pauseLabel, $"regular:{pauseAction}:{paymentId}"),
            InlineKeyboardButton.WithCallbackData("🗑 Удалить", $"regular:delete:{paymentId}") 
        });

        buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("🔙 Назад", "regular:main") });
        return new InlineKeyboardMarkup(buttons);
    }

    // История
    public static InlineKeyboardMarkup History(int paymentId) => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("🔙 К платежу", $"regular:detail:{paymentId}") }
    });

    // Подтверждение удаления
    public static InlineKeyboardMarkup DeleteConfirm(int paymentId) => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("🗑 Да, удалить", $"regular:delete:confirm:{paymentId}") },
        new[] { InlineKeyboardButton.WithCallbackData("🔙 Отмена", $"regular:detail:{paymentId}") }
    });

    // После создания
    public static InlineKeyboardMarkup AfterCreate() => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("📋 Все платежи", "regular:list") },
        new[] { InlineKeyboardButton.WithCallbackData("🔙 В меню", "menu:main") }
    });

    // После оплаты
    public static InlineKeyboardMarkup AfterPay(int paymentId) => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("📋 Все платежи", "regular:list") },
        new[] { InlineKeyboardButton.WithCallbackData("🔙 В меню", "menu:main") }
    });

    // Редактирование
    public static InlineKeyboardMarkup Edit(int paymentId) => new(new[]
    {
        new[] 
        { 
            InlineKeyboardButton.WithCallbackData("📝 Название", $"regular:edit:name:{paymentId}"),
            InlineKeyboardButton.WithCallbackData("💰 Сумму", $"regular:edit:amount:{paymentId}")
        },
        new[] 
        { 
            InlineKeyboardButton.WithCallbackData("📅 Дату", $"regular:edit:day:{paymentId}")
        },
        new[] { InlineKeyboardButton.WithCallbackData("🔙 Назад", $"regular:detail:{paymentId}") }
    });
}
