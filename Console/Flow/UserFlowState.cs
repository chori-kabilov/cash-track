using Domain.Enums;

namespace Console.Flow;

// Состояние диалога с пользователем (хранится в памяти)
public sealed class UserFlowState
{
    public UserFlowStep Step { get; set; }
    public TransactionType PendingType { get; set; }
    public decimal PendingAmount { get; set; }
    public int? PendingCategoryId { get; set; }
    public bool PendingIsImpulsive { get; set; }
    public string? PendingDescription { get; set; }
    public int? PendingTransactionId { get; set; }
    
    public string? PendingGoalName { get; set; }
    public decimal PendingGoalTarget { get; set; }
    public DateTimeOffset? PendingGoalDeadline { get; set; }
    public int? PendingGoalId { get; set; }

    public string? PendingDebtName { get; set; }
    public decimal PendingDebtAmount { get; set; }
    public DebtType? PendingDebtType { get; set; }
    public int? PendingDebtId { get; set; }
    public DateTimeOffset? PendingDebtDeadline { get; set; }
    public string? PendingDebtDescription { get; set; }
    public bool? PendingAddToBalance { get; set; }

    public string? PendingRegularName { get; set; }
    public decimal PendingRegularAmount { get; set; }
    public PaymentFrequency PendingRegularFrequency { get; set; }

    public int? PendingLimitCategoryId { get; set; }
    public int? PendingMessageId { get; set; }
    
    // Баланс — состояния переключателей
    public bool BalanceShowDebts { get; set; } = false;  // ВЫКЛ по умолчанию
    public bool BalanceShowGoals { get; set; } = true;   // ВКЛ по умолчанию
    public bool BalanceShowPayments { get; set; } = true; // ВКЛ по умолчанию
    
    // Статистика — состояние навигации
    public DateTimeOffset StatsDate { get; set; } = DateTimeOffset.UtcNow;  // Текущая дата периода
    public StatsPeriod StatsPeriod { get; set; } = StatsPeriod.Month;       // Неделя/Месяц/Год
    public StatsScreen CurrentStatsScreen { get; set; } = StatsScreen.Summary;
    public int StatsPage { get; set; } = 1;              // Пагинация истории
    public bool StatsShowExpenses { get; set; } = true;  // Категории: расходы/доходы
    
    // Цели — состояние хаба
    public GoalScreen CurrentGoalScreen { get; set; } = GoalScreen.Main;    // Текущий экран
    public int? OldGoalIdForTransfer { get; set; }  // ID старой цели при смене приоритета (для трансфера)
    public int PendingListPage { get; set; } = 0;   // Страница пагинации списка целей
}