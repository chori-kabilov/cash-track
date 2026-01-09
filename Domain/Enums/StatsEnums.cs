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
