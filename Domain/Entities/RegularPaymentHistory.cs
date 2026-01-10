using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Domain.Entities;

// История оплат регулярных платежей
[Table("regular_payment_histories")]
public class RegularPaymentHistory
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int RegularPaymentId { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Required]
    public DateTimeOffset PaidAt { get; set; }

    public int? TransactionId { get; set; } // Связь с транзакцией

    // Navigation
    [ForeignKey(nameof(RegularPaymentId))]
    public RegularPayment RegularPayment { get; set; } = null!;

    [ForeignKey(nameof(TransactionId))]
    public Transaction? Transaction { get; set; }
}
