using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniVerein.DAL.Entities;

[Table("UserDevices")]
public class UserDeviceEntity : TenantScopedEntity
{
    [Column("user_id")]
    public Guid UserId { get; set; }

    [ForeignKey("UserId")]
    public UserEntity? User { get; set; }

    [Column("token_hash")]
    public required string TokenHash { get; set; } = string.Empty;

    [Column("last_seen_at")]
    public DateTimeOffset LastSeenAt { get; set; }
}
