using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Domain.Enums;

namespace Domain.Entities;

[Table("transactions")]
public class Transaction
{
    [Key]
    public int Id { get; set; } // Уникальный идентификатор транзакции

    [Required]
    public int AccountId { get; set; } // ID счета, с которого/на который

    [Required]
    public int CategoryId { get; set; } // ID категории транзакции

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; } // Сумма транзакции (всегда положительная)

    [Required]
    public TransactionType Type { get; set; } // Тип: Доход или Расход

    [MaxLength(500)]
    public string? Description { get; set; } // Описание транзакции
    public bool IsImpulsive { get; set; } // "На эмоциях" - эмоциональная /незапланированная покупка
    public bool IsDraft { get; set; } // Черновик (не учитывается в балансе)
    public bool IsError { get; set; } // Ошибочная транзакция (отменена)

    [Required]
    public DateTimeOffset Date { get; set; } // Дата совершения транзакции

    [Required]
    public DateTimeOffset CreatedAt { get; set; } // Дата создания записи в системе

    // Soft Delete
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation properties
    [ForeignKey(nameof(AccountId))]
    public Account Account { get; set; } = null!; // Ссылка на счет

    [ForeignKey(nameof(CategoryId))]
    public Category Category { get; set; } = null!; // Ссылка на категорию
}

