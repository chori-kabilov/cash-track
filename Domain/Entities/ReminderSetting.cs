using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("reminder_settings")]
public class ReminderSetting
{
    [Key]
    public int Id { get; set; }

    [Required]
    public long UserId { get; set; }

    [Required]
    [MaxLength(20)]
    public string Frequency { get; set; } = "daily"; // daily | weekly

    [Required]
    public TimeSpan TimeOfDay { get; set; }

    public DayOfWeek? DayOfWeek { get; set; } // only for weekly

    public bool Enabled { get; set; } = true;

    public DateTimeOffset? LastNotifiedAtUtc { get; set; }

    [ForeignKey(nameof(UserId))]
    public User User { get; set; } = null!;
}
