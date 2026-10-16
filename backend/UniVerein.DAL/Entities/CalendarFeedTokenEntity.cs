using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniVerein.DAL.Entities;

// Secret token of a user's personal iCal feed. Only the SHA-256 hash is stored, the token itself is shown to
// the user once when it is created.
[Table("CalendarFeedTokens")]
public class CalendarFeedTokenEntity : BaseEntity
{
    [Column("user_id")]
    public Guid UserId { get; set; }

    [ForeignKey("UserId")]
    public UserEntity? User { get; set; }

    [Column("token_hash")]
    public required string TokenHash { get; set; } = string.Empty;

    [Column("last_used_at")]
    public DateTimeOffset? LastUsedAt { get; set; }
}
