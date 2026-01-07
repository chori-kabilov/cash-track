using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("goals")]
public class Goal
{
    [Key]
    public int Id { get; set; } // Уникальный идентификатор цели

    [Required]
    public long UserId { get; set; } // ID пользователя-владельца

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty; // Название цели (например, "Отпуск")

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal TargetAmount { get; set; } // Целевая сумма

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal CurrentAmount { get; set; } // Накопленная сумма
    public DateTimeOffset? Deadline { get; set; } // Дедлайн (если установлен)
    public int Priority { get; set; } = 1; // Приоритет цели (1 = высший)
    public bool IsActive { get; set; } = true; // Активна ли цель
    public bool IsCompleted { get; set; } // Достигнута ли цель
    public DateTimeOffset? CompletedAt { get; set; } // Дата достижения цели

    [Required]
    public DateTimeOffset CreatedAt { get; set; } // Дата создания записи
    public bool IsDeleted { get; set; } // Флаг: удалена ли запись
    public DateTimeOffset? DeletedAt { get; set; } // Дата удаления записи

    // Navigation properties
    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!; // Ссылка на пользователя
}
