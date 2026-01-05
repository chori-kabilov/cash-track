using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Domain.Enums;

namespace Domain.Entities;

[Table("categories")]
public class Category
{
    [Key]
    public int Id { get; set; }

    [Required]
    public long UserId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Icon { get; set; }

    /// <summary>
    /// Type of transactions this category is for (Income or Expense).
    /// Null means category can be used for both types.
    /// </summary>
    public TransactionType? Type { get; set; }

    /// <summary>
    /// Priority/importance level of this category.
    /// </summary>
    public Priority Priority { get; set; } = Priority.Optional;

    /// <summary>
    /// Parent category ID for subcategories.
    /// </summary>
    public int? ParentCategoryId { get; set; }

    /// <summary>
    /// Is this category active (soft delete).
    /// </summary>
    public bool IsActive { get; set; } = true;

    [Required]
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation properties
    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;

    [ForeignKey(nameof(ParentCategoryId))]
    public Category? ParentCategory { get; set; }

    public List<Category> SubCategories { get; set; } = new();

    public List<Transaction> Transactions { get; set; } = new();
}

