using Domain.Enums;

internal sealed class UserFlowState
{
    public UserFlowStep Step { get; set; }
    public TransactionType PendingType { get; set; }
    public decimal PendingAmount { get; set; }
    public int? PendingCategoryId { get; set; }
    public bool PendingIsImpulsive { get; set; }
    
    public string? PendingGoalName { get; set; }
    public decimal PendingGoalTarget { get; set; }
    public DateTimeOffset? PendingGoalDeadline { get; set; }
    public int? PendingGoalId { get; set; }

    public string? PendingDebtName { get; set; }
    public decimal PendingDebtAmount { get; set; }
    public DebtType PendingDebtType { get; set; }
    public int? PendingDebtId { get; set; }

    public string? PendingRegularName { get; set; }
    public decimal PendingRegularAmount { get; set; }
    public PaymentFrequency PendingRegularFrequency { get; set; }
}