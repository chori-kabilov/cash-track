using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("reminder_settings")]
public class ReminderSetting
{
    [Key]
    public int Id { get; set; } // Уникальный идентификатор настройки напоминаний

    [Required]
    public long UserId { get; set; } // ID пользователя

    [Required]
    [MaxLength(20)]
    public string Frequency { get; set; } = "daily"; // Частота: daily | weekly

    [Required]
    public TimeSpan TimeOfDay { get; set; } // Время суток для отправки напоминания
    public DayOfWeek? DayOfWeek { get; set; } // День недели (только для weekly)
    public bool Enabled { get; set; } = true; // Включены ли напоминания
    public DateTimeOffset? LastNotifiedAtUtc { get; set; } // Время последнего уведомления

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!; // Ссылка на пользователя
}
