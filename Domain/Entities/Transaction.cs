using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Domain.Enums;

namespace Domain.Entities;

[Table("transactions")]
public class Transaction
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int AccountId { get; set; }

    [Required]
    public int CategoryId { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Required]
    public TransactionType Type { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// Was this an impulse purchase.
    /// </summary>
    public bool IsImpulsive { get; set; }

    /// <summary>
    /// Is this a draft (no description provided yet).
    /// </summary>
    public bool IsDraft { get; set; }

    /// <summary>
    /// Is this marked as an error (not deleted, just flagged).
    /// </summary>
    public bool IsError { get; set; }

    [Required]
    public DateTimeOffset Date { get; set; }

    [Required]
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation properties
    [ForeignKey(nameof(AccountId))]
    public Account Account { get; set; } = null!;

    [ForeignKey(nameof(CategoryId))]
    public Category Category { get; set; } = null!;
}

