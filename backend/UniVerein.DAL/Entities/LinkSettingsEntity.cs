using System.ComponentModel.DataAnnotations.Schema;

namespace UniVerein.DAL.Entities;

[Table("LinkSettings")]
public class LinkSettingsEntity : BaseEntity
{
    [Column("link")] 
    public string Link { get; set; } = string.Empty;

    [Column("icon")] 
    public string Icon { get; set; } = string.Empty;

    [Column("name")] 
    public string Name { get; set; } = string.Empty;
}