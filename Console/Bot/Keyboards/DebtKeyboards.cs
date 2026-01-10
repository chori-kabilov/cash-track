using Domain.Entities;
using Telegram.Bot.Types.ReplyMarkups;

namespace Console.Bot.Keyboards;

// Клавиатуры для управления долгами
public static class DebtKeyboards
{
    // Главный экран долгов (дашборд)
    public static InlineKeyboardMarkup Dashboard() => new(new[]
    {
        new[] 
        { 
            InlineKeyboardButton.WithCallbackData("📥 Мне должны", "debt:list:theyowe"),
            InlineKeyboardButton.WithCallbackData("📤 Я должен", "debt:list:iowe")
        },
        new[] 
        { 
            InlineKeyboardButton.WithCallbackData("➕ Новый долг", "debt:create"),
            InlineKeyboardButton.WithCallbackData("🔙 Назад", "menu:main")
        }
    });

    // Пустой экран
    public static InlineKeyboardMarkup Empty() => new(new[]
    {
        new[]
        {
            InlineKeyboardButton.WithCallbackData("📥 Мне должны", "debt:create:theyowe"),
            InlineKeyboardButton.WithCallbackData("📤 Я должен", "debt:create:iowe")
        },
        new[] { InlineKeyboardButton.WithCallbackData("🔙 Назад", "menu:main") }
    });

    // Выбор типа долга
    public static InlineKeyboardMarkup CreateType() => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("📥 Мне должны", "debt:create:theyowe") },
        new[] { InlineKeyboardButton.WithCallbackData("📤 Я должен", "debt:create:iowe") },
        new[] { InlineKeyboardButton.WithCallbackData("🔙 Отмена", "debt:main") }
    });

    // Отмена
    public static InlineKeyboardMarkup Cancel() => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("❌ Отмена", "debt:main") }
    });

    // Пропуск (для необязательных полей)
    public static InlineKeyboardMarkup Skip(string skipCallback) => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("⏭ Пропустить", skipCallback) },
        new[] { InlineKeyboardButton.WithCallbackData("❌ Отмена", "debt:main") }
    });

    // Добавить к балансу? (для "Я должен")
    public static InlineKeyboardMarkup AddToBalance() => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("✅ Да, добавить к балансу", "debt:addbalance:yes") },
        new[] { InlineKeyboardButton.WithCallbackData("❌ Нет", "debt:addbalance:no") }
    });

    // Список долгов с пагинацией
    public static InlineKeyboardMarkup List(int page, int totalPages, string type)
    {
        var buttons = new List<InlineKeyboardButton[]>();

        // Пагинация
        var navRow = new List<InlineKeyboardButton>();
        if (page > 0)
            navRow.Add(InlineKeyboardButton.WithCallbackData("⬅️", $"debt:list:{type}:{page - 1}"));
        else
            navRow.Add(InlineKeyboardButton.WithCallbackData(" ", "debt:noop"));
        navRow.Add(InlineKeyboardButton.WithCallbackData($"{page + 1}/{totalPages}", "debt:noop"));
        if (page < totalPages - 1)
            navRow.Add(InlineKeyboardButton.WithCallbackData("➡️", $"debt:list:{type}:{page + 1}"));
        else
            navRow.Add(InlineKeyboardButton.WithCallbackData(" ", "debt:noop"));
        buttons.Add(navRow.ToArray());

        // Управление
        var createType = type == "theyowe" ? "theyowe" : "iowe";
        buttons.Add(new[] 
        { 
            InlineKeyboardButton.WithCallbackData("➕ Добавить", $"debt:create:{createType}"),
            InlineKeyboardButton.WithCallbackData("🔙 Назад", "debt:main")
        });
        return new InlineKeyboardMarkup(buttons);
    }

    // Карточка долга
    public static InlineKeyboardMarkup Detail(int debtId, bool isTheyOwe)
    {
        var payLabel = isTheyOwe ? "💵 Получить платёж" : "💵 Внести платёж";
        return new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData(payLabel, $"debt:pay:{debtId}") },
            new[] { InlineKeyboardButton.WithCallbackData("📜 История платежей", $"debt:history:{debtId}") },
            new[] 
            { 
                InlineKeyboardButton.WithCallbackData("✏️ Редактировать", $"debt:edit:{debtId}"),
                InlineKeyboardButton.WithCallbackData("🗑 Удалить", $"debt:delete:{debtId}")
            },
            new[] { InlineKeyboardButton.WithCallbackData("🔙 Назад", "debt:main") }
        });
    }

    // Подтверждение удаления
    public static InlineKeyboardMarkup DeleteConfirm(int debtId) => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("🗑 Да, удалить", $"debt:delete:confirm:{debtId}") },
        new[] { InlineKeyboardButton.WithCallbackData("🔙 Отмена", $"debt:detail:{debtId}") }
    });

    // История платежей
    public static InlineKeyboardMarkup History(int debtId, bool isTheyOwe)
    {
        var payLabel = isTheyOwe ? "💵 Получить платёж" : "💵 Внести платёж";
        return new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData(payLabel, $"debt:pay:{debtId}") },
            new[] { InlineKeyboardButton.WithCallbackData("🔙 К долгу", $"debt:detail:{debtId}") }
        });
    }

    // После создания долга
    public static InlineKeyboardMarkup AfterCreate() => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("📋 Все долги", "debt:main") },
        new[] { InlineKeyboardButton.WithCallbackData("🔙 В меню", "menu:main") }
    });

    // После платежа (есть остаток)
    public static InlineKeyboardMarkup AfterPayment(int debtId, bool isTheyOwe)
    {
        var payLabel = isTheyOwe ? "💵 Ещё платёж" : "💵 Ещё платёж";
        return new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData(payLabel, $"debt:pay:{debtId}") },
            new[] { InlineKeyboardButton.WithCallbackData("📋 К долгу", $"debt:detail:{debtId}") },
            new[] { InlineKeyboardButton.WithCallbackData("🔙 Все долги", "debt:main") }
        });
    }

    // После полного погашения
    public static InlineKeyboardMarkup AfterFullPayment() => new(new[]
    {
        new[] { InlineKeyboardButton.WithCallbackData("📋 Все долги", "debt:main") },
        new[] { InlineKeyboardButton.WithCallbackData("🔙 В меню", "menu:main") }
    });

    // Редактирование
    public static InlineKeyboardMarkup Edit(int debtId) => new(new[]
    {
        new[] 
        { 
            InlineKeyboardButton.WithCallbackData("👤 Имя", $"debt:edit:name:{debtId}"),
            InlineKeyboardButton.WithCallbackData("📅 Дедлайн", $"debt:edit:deadline:{debtId}")
        },
        new[] { InlineKeyboardButton.WithCallbackData("📝 Описание", $"debt:edit:desc:{debtId}") },
        new[] { InlineKeyboardButton.WithCallbackData("🔙 Назад", $"debt:detail:{debtId}") }
    });
}
