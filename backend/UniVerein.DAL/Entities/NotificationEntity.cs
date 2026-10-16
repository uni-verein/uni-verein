using System;
using System.ComponentModel.DataAnnotations.Schema;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.DAL.Entities;

[Table("Notifications")]
public class NotificationEntity : BaseEntity
{
    [Column("user_id")]
    public Guid UserId { get; set; }

    [ForeignKey("UserId")]
    public UserEntity? User { get; set; }

    [Column("type")]
    public NotificationType Type { get; set; }

    [Column("event_id")]
    public Guid? EventId { get; set; }

    [Column("event_title")]
    public string EventTitle { get; set; } = string.Empty;

    [Column("event_start")]
    public DateTimeOffset? EventStart { get; set; }

    [Column("actor_username")]
    public string ActorUsername { get; set; } = string.Empty;

    // Comma separated list of changed fields (e.g. "title,start") or the RSVP status for CALENDAR_RSVP.
    [Column("details")]
    public string Details { get; set; } = string.Empty;

    [Column("read_at")]
    public DateTimeOffset? ReadAt { get; set; }
}
