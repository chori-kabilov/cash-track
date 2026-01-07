using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Domain.Enums;

namespace Domain.Entities;

[Table("regular_payments")]
public class RegularPayment
{
    [Key]
    public int Id { get; set; } // Уникальный идентификатор регулярного платежа

    [Required]
    public long UserId { get; set; } // ID пользователя-владельца

    public int? CategoryId { get; set; } // ID категории (опционально)

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty; // Название (например, "Интернет")

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; } // Сумма платежа

    [Required]
    public PaymentFrequency Frequency { get; set; } // Частота (ежедневно/еженедельно/ежемесячно)
    public int? DayOfMonth { get; set; } // День месяца (для ежемесячных)
    public int? DayOfWeek { get; set; } // День недели (для еженедельных)
    public int ReminderDaysBefore { get; set; } = 3; // За сколько дней напомнить
    public bool IsPaused { get; set; } // Приостановлен ли платеж
    public DateTimeOffset? LastPaidDate { get; set; } // Дата последней оплаты
    public DateTimeOffset? NextDueDate { get; set; } // Дата следующего платежа
    public bool IsDeleted { get; set; } // Удалено ли платеж
    public DateTimeOffset? DeletedAt { get; set; } // Дата удаления записи

    [Required]
    public DateTimeOffset CreatedAt { get; set; } // Дата создания записи

    // Navigation properties
    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!; // Ссылка на пользователя

    [ForeignKey(nameof(CategoryId))]
    public Category? Category { get; set; } // Ссылка на категорию
}
