using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniVerein.DAL.Entities;

[Table("CalendarEventSubscriptions")]
public class CalendarEventSubscriptionEntity : BaseEntity
{
    [Column("event_id")]
    public Guid EventId { get; set; }

    [ForeignKey("EventId")]
    public CalendarEventEntity? Event { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [ForeignKey("UserId")]
    public UserEntity? User { get; set; }
}
