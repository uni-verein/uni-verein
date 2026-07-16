using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniVerein.DAL.Entities;

[Table("FirmwareVersions")]
public class FirmwareVersionEntity : BaseEntity
{
    [Column("version")] 
    public required string Version { get; set; }
    
    [Column("tag_name")] 
    public required string TagName { get; set; }
    
    [Column("release_notes")] 
    public string? ReleaseNotes { get; set; }
    
    [Column("published_at")] 
    public DateTimeOffset PublishedAt { get; set; }

    public ICollection<FirmwareVersionNotificationEntity> Notifications { get; set; } = new List<FirmwareVersionNotificationEntity>();   
}