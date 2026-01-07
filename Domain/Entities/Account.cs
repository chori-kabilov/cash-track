using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("accounts")]
public class Account
{
    [Key]
    public int Id { get; set; } // Уникальный идентификатор счета

    [Required]
    public long UserId { get; set; } // ID пользователя-владельца счета

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty; // Название счета (например, "Кошелек" или "Карта")

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Balance { get; set; } // Текущий баланс средств на счете

    [MaxLength(10)]
    public string Currency { get; set; } = "TJS"; // Валюта счета (по умолчанию Сомони)

    [Required]
    public DateTimeOffset CreatedAt { get; set; } // Дата создания записи

    [Required]
    public DateTimeOffset UpdatedAt { get; set; } // Дата последнего обновления записи

    // Navigation properties
    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!; // Ссылка на пользователя

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>(); // История транзакций по этому счету
}
