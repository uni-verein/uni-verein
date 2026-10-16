using System;
using System.Collections.Generic;
using UniVerein.DAL.Entities;

namespace UniVerein.Api.Services.Calendar;

public class CalendarDueReminder
{
    public CalendarEventEntity Event { get; }
    public DateTimeOffset OccurrenceStart { get; }
    public IReadOnlyCollection<Guid> Recipients { get; }

    public CalendarDueReminder(CalendarEventEntity calendarEvent, DateTimeOffset occurrenceStart,
        IReadOnlyCollection<Guid> recipients)
    {
        Event = calendarEvent;
        OccurrenceStart = occurrenceStart;
        Recipients = recipients;
    }
}
