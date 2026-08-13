using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniVerein.DAL.Entities;

[Table("ReceiptFiles")]
public class ReceiptFileEntity : BaseEntity
{
    [Column("receipt_id")]
    public Guid ReceiptId { get; set; }

    [ForeignKey("ReceiptId")]
    public required ReceiptEntity Receipt { get; set; }

    [Column("content_type")]
    public required string ContentType { get; set; } = string.Empty;

    [Column("position")]
    public int Position { get; set; }
}
