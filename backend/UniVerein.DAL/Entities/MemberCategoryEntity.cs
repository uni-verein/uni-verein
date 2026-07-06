using System.ComponentModel.DataAnnotations.Schema;

namespace UniVerein.DAL.Entities;

[Table("MemberCategories")]
public class MemberCategoryEntity : BaseEntity
{
    [Column("category")] 
    public required string Category { get; set; }

    [Column("name")] 
    public required string Name { get; set; }
}