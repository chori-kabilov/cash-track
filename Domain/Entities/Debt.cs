using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Debt tracking - money owed to or by others.
/// </summary>
[Table("debts")]
public class Debt
{
    [Key]
    public int Id { get; set; }

    [Required]
    public long UserId { get; set; }

    /// <summary>
    /// Name of the person involved in the debt.
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string PersonName { get; set; } = string.Empty;

    /// <summary>
    /// Original debt amount.
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    /// <summary>
    /// Remaining amount to be paid.
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal RemainingAmount { get; set; }

    /// <summary>
    /// Type of debt: I owe or they owe me.
    /// </summary>
    [Required]
    public DebtType Type { get; set; }

    /// <summary>
    /// Description of what the debt is for.
    /// </summary>
    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// When the debt was taken.
    /// </summary>
    [Required]
    public DateTimeOffset TakenDate { get; set; }

    /// <summary>
    /// Optional due date for repayment.
    /// </summary>
    public DateTimeOffset? DueDate { get; set; }

    /// <summary>
    /// Has the debt been fully paid.
    /// </summary>
    public bool IsPaid { get; set; }

    /// <summary>
    /// When the debt was fully paid.
    /// </summary>
    public DateTimeOffset? PaidAt { get; set; }

    [Required]
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation properties
    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;
}
