using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("users")]
public class User
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public long Id { get; set; } // Telegram ID (главный ключ)

    [Required]
    [MaxLength(255)]
    public string FirstName { get; set; } = string.Empty; // Имя пользователя

    [MaxLength(255)]
    public string? LastName { get; set; } // Фамилия (опционально)

    [MaxLength(255)]
    public string? Username { get; set; } // @username в Telegram

    [MaxLength(10)]
    public string? LanguageCode { get; set; } // Языковой код (ru, en)

    public bool? IsBot { get; set; } // Является ли ботом

    [MaxLength(50)]
    public string Timezone { get; set; } = "Asia/Dushanbe"; // Часовой пояс пользователя
    public bool IsBalanceHidden { get; set; } = true; // Скрыт ли баланс в меню
    public bool HasCompletedOnboarding { get; set; } // Прошел ли обучение

    [Column(TypeName = "decimal(18,2)")]
    public decimal? InitialBalance { get; set; } // Начальный баланс (при регистрации)

    public DateTimeOffset? LastMessageAt { get; set; } // Время последнего сообщения от пользователя

    [Required]
    public DateTimeOffset CreatedAt { get; set; } // Дата регистрации

    [Required]
    public DateTimeOffset UpdatedAt { get; set; } // Дата последнего изменения

    // Soft Delete
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    // Navigation properties
    public List<Account> Accounts { get; set; } = new(); // Счета пользователя
    public List<Category> Categories { get; set; } = new(); // Категории пользователя
    public List<Goal> Goals { get; set; } = new(); // Цели пользователя
    public List<Debt> Debts { get; set; } = new(); // Долги пользователя
    public List<RegularPayment> RegularPayments { get; set; } = new(); // Регулярные платежи
    public List<Limit> Limits { get; set; } = new(); // Лимиты пользователя
}

