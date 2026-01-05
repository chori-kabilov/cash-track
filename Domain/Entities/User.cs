using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

[Table("users")]
public class User
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public long Id { get; set; }

    [Required]
    [MaxLength(255)]
    public string FirstName { get; set; } = string.Empty;

    [MaxLength(255)]
    public string? LastName { get; set; }

    [MaxLength(255)]
    public string? Username { get; set; }

    [MaxLength(10)]
    public string? LanguageCode { get; set; }

    public bool? IsBot { get; set; }

    /// <summary>
    /// User's timezone (default: Asia/Dushanbe = UTC+5).
    /// </summary>
    [MaxLength(50)]
    public string Timezone { get; set; } = "Asia/Dushanbe";

    /// <summary>
    /// Hide balance until user explicitly asks.
    /// </summary>
    public bool IsBalanceHidden { get; set; } = true;

    /// <summary>
    /// Has user completed onboarding tutorial.
    /// </summary>
    public bool HasCompletedOnboarding { get; set; }

    /// <summary>
    /// Initial balance set during onboarding.
    /// </summary>
    [Column(TypeName = "decimal(18,2)")]
    public decimal? InitialBalance { get; set; }

    public DateTimeOffset? LastMessageAt { get; set; }

    [Required]
    public DateTimeOffset CreatedAt { get; set; }

    [Required]
    public DateTimeOffset UpdatedAt { get; set; }

    // Navigation properties
    public List<Account> Accounts { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
    public List<Goal> Goals { get; set; } = new();
    public List<Debt> Debts { get; set; } = new();
    public List<RegularPayment> RegularPayments { get; set; } = new();
    public List<Limit> Limits { get; set; } = new();
}

