namespace Domain.Enums;

/// <summary>
/// Frequency of regular payments.
/// </summary>
public enum PaymentFrequency
{
    /// <summary>
    /// Every day.
    /// </summary>
    Daily = 1,

    /// <summary>
    /// Every week.
    /// </summary>
    Weekly = 2,

    /// <summary>
    /// Every month on a specific day.
    /// </summary>
    Monthly = 3,

    /// <summary>
    /// Once a year.
    /// </summary>
    Yearly = 4,

    /// <summary>
    /// Custom frequency.
    /// </summary>
    Custom = 5
}
