using System;
using System.ComponentModel.DataAnnotations.Schema;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.DAL.Entities;

[Table("UserSettings")]
public class UserSettingEntity : BaseEntity
{
    [Column("user_id")]
    public Guid UserId { get; set; }

    [ForeignKey("UserId")]
    public required UserEntity User { get; set; }

    [Column("type")]
    public UserSettingType Type { get; set; }

    [Column("enabled")]
    public bool Enabled { get; set; }
}
