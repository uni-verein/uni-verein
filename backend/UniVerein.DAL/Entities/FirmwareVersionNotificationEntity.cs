using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniVerein.DAL.Entities;

[Table("FirmwareVersionNotifications")]
public class FirmwareVersionNotificationEntity : BaseEntity
{
    [Column("user_id")] 
    public Guid UserId { get; set; }
    
    [ForeignKey("UserId")]
    public required UserEntity User { get; set; }

    [Column("firmware_version_id")]
    public Guid FirmwareVersionId { get; set; }

    [ForeignKey("FirmwareVersionId")]
    public required FirmwareVersionEntity FirmwareVersion { get; set; }
}