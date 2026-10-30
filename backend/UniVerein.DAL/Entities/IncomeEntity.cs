using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniVerein.DAL.Entities;

[Table("Incomes")]
public class IncomeEntity : BaseEntity
{
    [Column("category_id")]
    public Guid? CategoryId { get; set; }

    [ForeignKey("CategoryId")]
    public IncomeCategoryEntity? Category { get; set; }

    [Column("payer")]
    public required string Payer { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("amount")]
    public decimal Amount { get; set; }

    [Column("income_date")]
    public DateTime IncomeDate { get; set; }

    [Column("paid")]
    public DateTimeOffset? Paid { get; set; }

    [Column("payment_date")]
    public DateTime? PaymentDate { get; set; }
}
