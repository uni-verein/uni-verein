using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniVerein.DAL.Entities;

[Table("UserRecoveryCodes")]
public class UserRecoveryCodeEntity : BaseEntity
{
    [Column("user_id")]
    public Guid UserId { get; set; }

    [ForeignKey("UserId")]
    public UserEntity? User { get; set; }

    [Column("code_hash")]
    public required string CodeHash { get; set; } = string.Empty;

    [Column("used_at")]
    public DateTimeOffset? UsedAt { get; set; }
}
