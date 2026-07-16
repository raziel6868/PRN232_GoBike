using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using BusinessObjects;
using BusinessObjects.Enums;

namespace BusinessObjects.Entities;

public class RentalPayment
{
    [Key]
    public int Id { get; set; }

    public int RentalContractId { get; set; }

    public PaymentType PaymentType { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    public PaymentMethod PaymentMethod { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }

    [MaxLength(500)]
    public string? ProofImageUrl { get; set; }

    public int? CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = SystemClock.Now;

    [ForeignKey(nameof(RentalContractId))]
    public RentalContract? RentalContract { get; set; }

    [ForeignKey(nameof(CreatedByUserId))]
    public User? CreatedByUser { get; set; }
}
