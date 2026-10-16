using System;

namespace UniVerein.Api.Services.Calendar;

public class CalendarOccurrence
{
    public DateTimeOffset Start { get; }
    public DateTimeOffset End { get; }

    public CalendarOccurrence(DateTimeOffset start, DateTimeOffset end)
    {
        Start = start;
        End = end;
    }
}
