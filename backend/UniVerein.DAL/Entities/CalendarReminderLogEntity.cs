using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniVerein.DAL.Entities;

// Marks that the reminder for one occurrence of an event was sent, so it is not sent again.
[Table("CalendarReminderLogs")]
public class CalendarReminderLogEntity : BaseEntity
{
    [Column("event_id")]
    public Guid EventId { get; set; }

    [Column("occurrence_start")]
    public DateTimeOffset OccurrenceStart { get; set; }
}
