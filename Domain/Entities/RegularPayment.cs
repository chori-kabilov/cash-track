using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Regular recurring payment.
/// </summary>
[Table("regular_payments")]
public class RegularPayment
{
    [Key]
    public int Id { get; set; }

    [Required]
    public long UserId { get; set; }

    /// <summary>
    /// Category for this payment.
    /// </summary>
    public int? CategoryId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    /// <summary>
    /// How often this payment occurs.
    /// </summary>
    [Required]
    public PaymentFrequency Frequency { get; set; }

    /// <summary>
    /// Day of month for monthly payments (1-31).
    /// </summary>
    public int? DayOfMonth { get; set; }

    /// <summary>
    /// Day of week for weekly payments (0-6, Sunday = 0).
    /// </summary>
    public int? DayOfWeek { get; set; }

    /// <summary>
    /// How many days before to send reminder.
    /// </summary>
    public int ReminderDaysBefore { get; set; } = 3;

    /// <summary>
    /// Is this payment temporarily paused.
    /// </summary>
    public bool IsPaused { get; set; }

    /// <summary>
    /// When was this payment last marked as paid.
    /// </summary>
    public DateTimeOffset? LastPaidDate { get; set; }

    /// <summary>
    /// Next due date for this payment.
    /// </summary>
    public DateTimeOffset? NextDueDate { get; set; }

    [Required]
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation properties
    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    [ForeignKey(nameof(CategoryId))]
    public Category? Category { get; set; }
}
