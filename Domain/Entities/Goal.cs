using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

/// <summary>
/// Savings goal for accumulating money.
/// </summary>
[Table("goals")]
public class Goal
{
    [Key]
    public int Id { get; set; }

    [Required]
    public long UserId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Target amount to save.
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal TargetAmount { get; set; }

    /// <summary>
    /// Current saved amount.
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal CurrentAmount { get; set; }

    /// <summary>
    /// Optional deadline for the goal.
    /// </summary>
    public DateTimeOffset? Deadline { get; set; }

    /// <summary>
    /// Priority of this goal in the queue.
    /// </summary>
    public int Priority { get; set; } = 1;

    /// <summary>
    /// Is this the active goal being saved for.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Has the goal been completed.
    /// </summary>
    public bool IsCompleted { get; set; }

    /// <summary>
    /// When the goal was completed.
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    [Required]
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation properties
    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;
}
