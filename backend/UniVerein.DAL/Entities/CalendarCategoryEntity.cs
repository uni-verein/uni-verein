using System.ComponentModel.DataAnnotations.Schema;

namespace UniVerein.DAL.Entities;

[Table("CalendarCategories")]
public class CalendarCategoryEntity : BaseEntity
{
    [Column("name")]
    public required string Name { get; set; } = string.Empty;

    [Column("color")]
    public required string Color { get; set; } = string.Empty;

    [Column("is_public")]
    public bool IsPublic { get; set; }
}
