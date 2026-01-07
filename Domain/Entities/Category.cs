using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Domain.Enums;

namespace Domain.Entities;

[Table("categories")]
public class Category
{
    [Key]
    public int Id { get; set; } // Уникальный идентификатор категории

    [Required]
    public long UserId { get; set; } // ID пользователя-владельца

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty; // Название категории

    [MaxLength(50)]
    public string? Icon { get; set; } // Иконка категории (например, эмодзи)

    public TransactionType? Type { get; set; } // Тип операций: Доход или Расход

    public Priority Priority { get; set; } = Priority.Optional; // Приоритет (Обязательно/Желательно/Дополнительно)

    public int? ParentCategoryId { get; set; } // ID родительской категории (для иерархии)

    public bool IsActive { get; set; } = true; // Активна ли категория (false = архив)

    [Required]
    public DateTimeOffset CreatedAt { get; set; } // Дата создания записи

    // Navigation properties
    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!; // Ссылка на пользователя

    [ForeignKey(nameof(ParentCategoryId))]
    public Category? ParentCategory { get; set; } // Ссылка на родительскую категорию

    public List<Category> SubCategories { get; set; } = new(); // Список подкатегорий

    public List<Transaction> Transactions { get; set; } = new(); // Транзакции по этой категории
}

