using System;
using System.Collections.Generic;
using System.Linq;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using UniVerein.DAL.Entities;
using IcalCalendar = Ical.Net.Calendar;

namespace UniVerein.Api.Services.Calendar;

public static class CalendarIcsBuilder
{
    public static string Build(string calendarName, IEnumerable<CalendarEventEntity> events, DateTimeOffset now,
        Func<CalendarEventEntity, string?>? linkFor = null)
    {
        List<CalendarEventEntity> list = events.ToList();
        IcalCalendar calendar = new() { Method = "PUBLISH" };
        calendar.AddProperty("X-WR-CALNAME", calendarName);
        CalendarProperty refreshInterval = new("REFRESH-INTERVAL", "PT1H");
        refreshInterval.Parameters.Add("VALUE", "DURATION");
        calendar.Properties.Add(refreshInterval);
        calendar.AddProperty("X-PUBLISHED-TTL", "PT1H");

        foreach (string timeZoneId in list.Where(e => !e.AllDay).Select(e => e.TimeZoneId).Distinct())
        {
            try
            {
                calendar.AddTimeZone(timeZoneId);
            }
            catch (Exception)
            {
                // Unknown zones fall back to UTC in CalendarRecurrence.ToCalDateTime.
            }
        }

        foreach (CalendarEventEntity calendarEvent in list)
            calendar.Events.Add(ToVEvent(calendarEvent, now, linkFor?.Invoke(calendarEvent)));

        return new CalendarSerializer().SerializeToString(calendar) ?? string.Empty;
    }

    private static CalendarEvent ToVEvent(CalendarEventEntity calendarEvent, DateTimeOffset now, string? link)
    {
        string description = calendarEvent.Description;
        if (!string.IsNullOrWhiteSpace(link))
            description = string.IsNullOrWhiteSpace(description) ? link : $"{description}\n\n{link}";

        string location = string.Join(", ", new[] { calendarEvent.LocationName, calendarEvent.LocationAddress }
            .Where(x => !string.IsNullOrWhiteSpace(x)));

        CalendarEvent vEvent = new()
        {
            Uid = $"{calendarEvent.Id}@uni-verein",
            Summary = calendarEvent.Title,
            Description = string.IsNullOrWhiteSpace(description) ? null : description,
            Location = string.IsNullOrWhiteSpace(location) ? null : location,
            DtStart = CalendarRecurrence.ToCalDateTime(calendarEvent, calendarEvent.StartAt),
            DtEnd = CalendarRecurrence.ToCalDateTime(calendarEvent, calendarEvent.EndAt),
            DtStamp = new CalDateTime(now.UtcDateTime, CalDateTime.UtcTzId),
            LastModified = new CalDateTime(calendarEvent.UpdatedAt.UtcDateTime, CalDateTime.UtcTzId),
            Sequence = calendarEvent.Version
        };

        if (Uri.TryCreate(calendarEvent.LocationUrl, UriKind.Absolute, out Uri? url))
            vEvent.Url = url;

        if (calendarEvent.Category != null)
            vEvent.Categories.Add(calendarEvent.Category.Name);

        if (!string.IsNullOrWhiteSpace(calendarEvent.RecurrenceRule))
        {
            vEvent.RecurrenceRule = new RecurrencePattern(calendarEvent.RecurrenceRule);
            foreach (DateTimeOffset exception in CalendarRecurrence.ParseExceptions(calendarEvent.RecurrenceExceptions))
                vEvent.ExceptionDates.Add(CalendarRecurrence.ToCalDateTime(calendarEvent, exception));
        }

        return vEvent;
    }
}
