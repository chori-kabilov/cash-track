using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

/// <summary>
/// Spending limit for a category.
/// </summary>
[Table("limits")]
public class Limit
{
    [Key]
    public int Id { get; set; }

    [Required]
    public long UserId { get; set; }

    [Required]
    public int CategoryId { get; set; }

    /// <summary>
    /// Maximum amount allowed for this category per period.
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    /// <summary>
    /// Amount spent in current period.
    /// </summary>
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal SpentAmount { get; set; }

    /// <summary>
    /// Period type: "month" for now.
    /// </summary>
    [Required]
    [MaxLength(20)]
    public string Period { get; set; } = "month";

    /// <summary>
    /// Start of current period.
    /// </summary>
    [Required]
    public DateTimeOffset PeriodStart { get; set; }

    /// <summary>
    /// Is the category currently blocked due to limit exceeded.
    /// </summary>
    public bool IsBlocked { get; set; }

    /// <summary>
    /// When the block expires.
    /// </summary>
    public DateTimeOffset? BlockedUntil { get; set; }

    /// <summary>
    /// Last warning level sent (50, 80, 100).
    /// </summary>
    public int LastWarningLevel { get; set; }

    [Required]
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation properties
    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    [ForeignKey(nameof(CategoryId))]
    public Category Category { get; set; } = null!;
}
