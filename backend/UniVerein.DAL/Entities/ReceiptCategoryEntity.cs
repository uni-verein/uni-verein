using System.ComponentModel.DataAnnotations.Schema;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.DAL.Entities;

[Table("ReceiptCategories")]
public class ReceiptCategoryEntity : BaseEntity
{
    [Column("name")]
    public required string Name { get; set; } = string.Empty;

    [Column("tax_area")]
    public TaxArea TaxArea { get; set; } = TaxArea.NON_PROFIT;
}
