using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniVerein.DAL.Entities;

[Table("PaymentAcknowledgements")]
public class PaymentAcknowledgementEntity : BaseEntity
{
    [Column("year")]
    public int Year { get; set; }

    [Column("sequence")]
    public int Sequence { get; set; }

    [Column("contribution_id")]
    public Guid? ContributionId { get; set; }

    [Column("income_id")]
    public Guid? IncomeId { get; set; }

    [Column("payer_name")]
    public required string PayerName { get; set; }

    [Column("payer_address")]
    public string? PayerAddress { get; set; }

    [Column("amount")]
    public decimal Amount { get; set; }

    [Column("purpose")]
    public required string Purpose { get; set; }

    [Column("paid_at")]
    public DateTimeOffset PaidAt { get; set; }

    [Column("issuer_name")]
    public required string IssuerName { get; set; }

    [Column("issuer_address")]
    public string? IssuerAddress { get; set; }

    [NotMapped]
    public string Number => $"{Year}-{Sequence:D4}";
}
