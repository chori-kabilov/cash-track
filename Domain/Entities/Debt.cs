using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Domain.Enums;

namespace Domain.Entities;

[Table("debts")]
public class Debt
{
    [Key]
    public int Id { get; set; } // Уникальный идентификатор долга

    [Required]
    public long UserId { get; set; } // ID пользователя-владельца

    [Required]
    [MaxLength(100)]
    public string PersonName { get; set; } = string.Empty; // Имя человека (кому должны или кто должен)

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; } // Изначальная сумма долга

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal RemainingAmount { get; set; } // Оставшаяся сумма (для частичного погашения)

    [Required]
    public DebtType Type { get; set; } // Тип: "Я должен" или "Мне должны"

    [MaxLength(500)]
    public string? Description { get; set; } // Дополнительное описание или заметка

    [Required]
    public DateTimeOffset TakenDate { get; set; } // Дата, когда был взят долг
    public DateTimeOffset? DueDate { get; set; } // Крайний срок возврата (если есть)
    public bool IsPaid { get; set; } // Флаг: полностью ли погашен долг
    public DateTimeOffset? PaidAt { get; set; } // Дата фактического полного погашения
    public bool IsDeleted { get; set; } // Флаг: удалена ли запись
    public DateTimeOffset? DeletedAt { get; set; } // Дата удаления записи
    [Required]
    public DateTimeOffset CreatedAt { get; set; } // Дата создания записи в системе

    // Navigation properties
    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!; // Ссылка на пользователя
}
