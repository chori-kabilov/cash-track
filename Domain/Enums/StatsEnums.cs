namespace Domain.Enums;

// Период статистики
public enum StatsPeriod
{
    Week = 1,
    Month = 2,
    Year = 3
}

// Текущий экран статистики
public enum StatsScreen
{
    Summary = 1,     // Главная сводка
    Categories = 2,  // Все категории
    History = 3,     // История операций
    Emotions = 4,    // Эмоциональные траты
    Regular = 5,     // Регулярные платежи
    PeriodSelect = 6 // Выбор периода
}

// Текущий экран целей
public enum GoalScreen
{
    Main = 1,           // Карточка главной цели
    List = 2,           // Список всех целей (смена приоритета)
    Transfer = 3,       // Диалог переноса денег
    Deposit = 4,        // Пополнение
    Withdraw = 5,       // Снятие
    CreateName = 6,     // Wizard: название
    CreateAmount = 7,   // Wizard: сумма
    CreateInitial = 8,  // Wizard: начальная сумма
    Settings = 9,       // Настройки цели
    Victory = 10        // Победа!
}
