using System;
using System.ComponentModel.DataAnnotations.Schema;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.DAL.Entities;

[Table("CalendarEventParticipants")]
public class CalendarEventParticipantEntity : BaseEntity
{
    [Column("event_id")]
    public Guid EventId { get; set; }

    [ForeignKey("EventId")]
    public CalendarEventEntity? Event { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [ForeignKey("UserId")]
    public UserEntity? User { get; set; }

    [Column("role")]
    public CalendarParticipantRole Role { get; set; } = CalendarParticipantRole.ATTENDEE;

    [Column("rsvp_status")]
    public CalendarRsvpStatus RsvpStatus { get; set; } = CalendarRsvpStatus.PENDING;
}
