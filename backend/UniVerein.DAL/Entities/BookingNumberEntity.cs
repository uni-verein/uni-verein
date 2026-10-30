using System;
using System.ComponentModel.DataAnnotations.Schema;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.DAL.Entities;

[Table("BookingNumbers")]
public class BookingNumberEntity : BaseEntity
{
    [Column("year")]
    public int Year { get; set; }

    [Column("kind")]
    public BookingKind Kind { get; set; }

    [Column("sequence")]
    public int Sequence { get; set; }

    [Column("contribution_id")]
    public Guid? ContributionId { get; set; }

    [Column("income_id")]
    public Guid? IncomeId { get; set; }

    [Column("receipt_id")]
    public Guid? ReceiptId { get; set; }

    [NotMapped]
    public string Number => $"{(Kind == BookingKind.INCOME ? "E" : "A")}-{Year}-{Sequence:D4}";
}
