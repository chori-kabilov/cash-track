using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

// История платежей по долгу
[Table("debt_payments")]
public class DebtPayment
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int DebtId { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Required]
    public DateTimeOffset PaidAt { get; set; }

    public int? TransactionId { get; set; } // Связь с транзакцией

    // Navigation
    [ForeignKey(nameof(DebtId))]
    public Debt Debt { get; set; } = null!;

    [ForeignKey(nameof(TransactionId))]
    public Transaction? Transaction { get; set; }
}
