using System.ComponentModel.DataAnnotations.Schema;

namespace UniVerein.DAL.Entities;

[Table("SepaExport")]
public class SepaExportEntity : TenantScopedEntity
{
    [Column("name")]
    public required string Name { get; set; }

    [Column("amount")]
    public required decimal Amount { get; set; }

    [Column("exportedCases")]
    public required int Count { get; set; }
}