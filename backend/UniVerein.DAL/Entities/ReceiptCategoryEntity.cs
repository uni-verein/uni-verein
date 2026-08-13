using System.ComponentModel.DataAnnotations.Schema;

namespace UniVerein.DAL.Entities;

[Table("ReceiptCategories")]
public class ReceiptCategoryEntity : BaseEntity
{
    [Column("name")]
    public required string Name { get; set; } = string.Empty;
}
