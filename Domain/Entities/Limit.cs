using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("limits")]
public class Limit
{
    [Key]
    public int Id { get; set; } // Уникальный идентификатор лимита

    [Required]
    public long UserId { get; set; } // ID пользователя-владельца

    [Required]
    public int CategoryId { get; set; } // ID категории, к которой применен лимит

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; } // Максимальная сумма лимита

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal SpentAmount { get; set; } // Потраченная сумма за период

    [Required]
    [MaxLength(20)]
    public string Period { get; set; } = "month"; // Период лимита (day/week/month/year)

    [Required]
    public DateTimeOffset PeriodStart { get; set; } // Начало текущего периода
    public bool IsBlocked { get; set; } // Заблокирован ли лимит (превышение)
    public DateTimeOffset? BlockedUntil { get; set; } // До какой даты заблокирован
    public int LastWarningLevel { get; set; } // Уровень последнего предупреждения (50%, 80%, 100%)
    public bool IsDeleted { get; set; } // Удалено ли лимит
    public DateTimeOffset? DeletedAt { get; set; } // Дата удаления записи
    
    [Required]
    public DateTimeOffset CreatedAt { get; set; } // Дата создания записи

    // Navigation properties
    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!; // Ссылка на пользователя

    [ForeignKey(nameof(CategoryId))]
    public Category Category { get; set; } = null!; // Ссылка на категорию
}
