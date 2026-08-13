using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.DAL.Entities;

[Table("Receipts")]
public class ReceiptEntity : BaseEntity
{
    [Column("user_id")]
    public Guid UserId { get; set; }

    [ForeignKey("UserId")]
    public UserEntity? User { get; set; }

    [Column("amount")]
    public decimal Amount { get; set; }

    [Column("receipt_date")]
    public DateTime ReceiptDate { get; set; }

    [Column("category_id")]
    public Guid? CategoryId { get; set; }

    [ForeignKey("CategoryId")]
    public ReceiptCategoryEntity? Category { get; set; }

    [Column("vendor")]
    public string? Vendor { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Column("payment_method")]
    public ReceiptPaymentMethod? PaymentMethod { get; set; }

    [Column("paid")]
    public DateTimeOffset? Paid { get; set; }

    public List<ReceiptFileEntity> Files { get; set; } = new();
}
