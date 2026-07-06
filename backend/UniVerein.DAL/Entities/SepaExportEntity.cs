using System.ComponentModel.DataAnnotations.Schema;

namespace UniVerein.DAL.Entities;

[Table("SepaExport")]
public class SepaExportEntity : BaseEntity
{
    [Column("name")] 
    public required string Name { get; set; }

    [Column("amount")]
    public required decimal Amount { get; set; }

    [Column("exportedCases")]
    public required int Count { get; set; }
}