namespace Console.Flow;

// Шаги диалогового потока с пользователем
public enum UserFlowStep
{
    None = 0,
    WaitingAmount = 1,
    ChoosingCategory = 2,
    WaitingDescription = 3,
    WaitingGoalName = 4,
    WaitingGoalTarget = 5,
    WaitingGoalDeadline = 6,
    WaitingGoalDeposit = 7,
    WaitingGoalWithdraw = 8,
    WaitingGoalSelect = 9,
    WaitingGoalEditName = 10,
    WaitingGoalEditAmount = 11,
    WaitingGoalEditDeadline = 12,
    WaitingDebtName = 13,
    WaitingDebtAmount = 14,
    WaitingDebtType = 15,
    WaitingDebtDeadline = 16,
    WaitingDebtPayment = 17,
    WaitingRegularName = 18,
    WaitingRegularAmount = 19,
    WaitingRegularFrequency = 20,
    WaitingRegularDate = 21,
    WaitingLimitCategory = 22,
    WaitingLimitAmount = 23,
    WaitingNewCategory = 24
}