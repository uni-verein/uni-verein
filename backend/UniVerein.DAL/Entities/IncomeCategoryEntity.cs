using System.ComponentModel.DataAnnotations.Schema;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.DAL.Entities;

[Table("IncomeCategories")]
public class IncomeCategoryEntity : BaseEntity
{
    [Column("name")]
    public required string Name { get; set; } = string.Empty;

    [Column("tax_area")]
    public TaxArea TaxArea { get; set; } = TaxArea.NON_PROFIT;
}
