using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Evaluation;
using UniVerein.DAL.Entities;
using Serilog;
using IcalFrequency = Ical.Net.FrequencyType;

namespace UniVerein.Api.Services.Calendar;

// Expands recurring events with Ical.Net. Timed events recur in their own time zone, so an event at 18:00
// stays at 18:00 local time across daylight saving changes.
public static class CalendarRecurrence
{
    // Used as series end of endless series, so time range queries can stay simple comparisons.
    public static readonly DateTimeOffset EndlessSeriesEnd = new(9999, 12, 31, 0, 0, 0, TimeSpan.Zero);

    // Expansion starts at the series start, so a series must not start unreasonably long ago.
    public static readonly DateTimeOffset EarliestSeriesStart = new(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);

    // Upper bound of occurrences produced per event and query, protects against huge expansions.
    private const int MaxOccurrences = 1000;

    private static readonly IcalFrequency[] AllowedFrequencies =
        [IcalFrequency.Daily, IcalFrequency.Weekly, IcalFrequency.Monthly, IcalFrequency.Yearly];

    public static bool IsValidRule(string? rule, out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(rule))
            return true;

        if (rule.Length > 500)
        {
            error = "The recurrence rule is too long.";
            return false;
        }

        try
        {
            RecurrencePattern pattern = new(rule);
            if (!AllowedFrequencies.Contains(pattern.Frequency))
            {
                error = "Only daily, weekly, monthly and yearly recurrences are supported.";
                return false;
            }

            if (pattern.Interval < 1 || pattern.Interval > 366)
            {
                error = "The recurrence interval must be between 1 and 366.";
                return false;
            }

            if (pattern.Count is < 1 or > 1000)
            {
                error = "The number of occurrences must be between 1 and 1000.";
                return false;
            }

            if (pattern.ByHour.Count > 0 || pattern.ByMinute.Count > 0 || pattern.BySecond.Count > 0)
            {
                error = "Recurrences by hour, minute or second are not supported.";
                return false;
            }
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException)
        {
            error = "The recurrence rule could not be parsed.";
            return false;
        }

        return true;
    }

    // Rules that parse but never match (e.g. 30 February) cannot be evaluated by Ical.Net. This runs the same
    // evaluations that are needed when an event is stored. Works on a copy, the event is not changed.
    public static bool IsEvaluable(CalendarEventEntity calendarEvent)
    {
        if (string.IsNullOrWhiteSpace(calendarEvent.RecurrenceRule))
            return true;

        CalendarEventEntity copy = new()
        {
            Title = calendarEvent.Title,
            StartAt = calendarEvent.StartAt,
            EndAt = calendarEvent.EndAt,
            AllDay = calendarEvent.AllDay,
            TimeZoneId = calendarEvent.TimeZoneId,
            RecurrenceRule = calendarEvent.RecurrenceRule
        };

        try
        {
            AlignStartToRule(copy);
            ComputeSeriesEnd(copy);
            return true;
        }
        catch (EvaluationException)
        {
            return false;
        }
    }

    public static List<DateTimeOffset> ParseExceptions(string exceptions)
    {
        return exceptions
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => DateTimeOffset.TryParse(x, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal,
                out DateTimeOffset value)
                ? value.ToUniversalTime()
                : (DateTimeOffset?)null)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToList();
    }

    public static string FormatExceptions(IEnumerable<DateTimeOffset> exceptions)
    {
        return string.Join(",", exceptions
            .Select(x => x.ToUniversalTime())
            .Distinct()
            .Order()
            .Select(x => x.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)));
    }

    // All occurrences that overlap [from, to).
    public static List<CalendarOccurrence> Expand(CalendarEventEntity calendarEvent, DateTimeOffset from,
        DateTimeOffset to)
    {
        TimeSpan duration = calendarEvent.EndAt - calendarEvent.StartAt;

        if (string.IsNullOrWhiteSpace(calendarEvent.RecurrenceRule))
        {
            return calendarEvent.StartAt < to && calendarEvent.EndAt > from
                ? [new CalendarOccurrence(calendarEvent.StartAt, calendarEvent.EndAt)]
                : [];
        }

        HashSet<DateTimeOffset> exceptions = ParseExceptions(calendarEvent.RecurrenceExceptions).ToHashSet();

        // Start early enough to also catch occurrences that began before 'from' but are still running.
        DateTimeOffset searchStart = from - duration - TimeSpan.FromDays(1);
        if (searchStart < calendarEvent.StartAt)
            searchStart = calendarEvent.StartAt;

        // One event with a rule that cannot be evaluated must not break lists and reminders of all other events.
        try
        {
            return Occurrences(calendarEvent, searchStart)
                .TakeWhile(start => start < to)
                .Where(start => !exceptions.Contains(start) && start + duration > from)
                .Take(MaxOccurrences)
                .Select(start => new CalendarOccurrence(start, start + duration))
                .ToList();
        }
        catch (EvaluationException ex)
        {
            Log.Warning(ex, $"CalendarRecurrence: Recurrence rule of calendar event {calendarEvent.Id} cannot be evaluated.");
            return [];
        }
    }

    public static DateTimeOffset ComputeSeriesEnd(CalendarEventEntity calendarEvent)
    {
        if (string.IsNullOrWhiteSpace(calendarEvent.RecurrenceRule))
            return calendarEvent.EndAt;

        RecurrencePattern pattern = new(calendarEvent.RecurrenceRule);
        if (pattern.Count == null && pattern.Until == null)
            return EndlessSeriesEnd;

        TimeSpan duration = calendarEvent.EndAt - calendarEvent.StartAt;
        DateTimeOffset? last = null;
        int count = 0;
        foreach (DateTimeOffset start in Occurrences(calendarEvent, calendarEvent.StartAt))
        {
            last = start;
            if (++count >= 5000)
                return EndlessSeriesEnd;
        }

        return last.HasValue ? last.Value + duration : calendarEvent.EndAt;
    }

    // Moves the start of a series to its first occurrence when the start does not match the rule (e.g. a Sunday
    // start for a series on Wednesdays). Calendar apps treat such a start inconsistently (RFC 5545 leaves it
    // undefined), so the stored series always starts with a real occurrence.
    public static void AlignStartToRule(CalendarEventEntity calendarEvent)
    {
        if (string.IsNullOrWhiteSpace(calendarEvent.RecurrenceRule))
            return;

        DateTimeOffset? first = Occurrences(calendarEvent, calendarEvent.StartAt)
            .Select(s => (DateTimeOffset?)s)
            .FirstOrDefault();
        if (first == null || first == calendarEvent.StartAt)
            return;

        TimeSpan shift = first.Value - calendarEvent.StartAt;
        calendarEvent.StartAt += shift;
        calendarEvent.EndAt += shift;
    }

    public static bool IsOccurrence(CalendarEventEntity calendarEvent, DateTimeOffset occurrenceStart)
    {
        DateTimeOffset start = occurrenceStart.ToUniversalTime();
        try
        {
            return Occurrences(calendarEvent, start.AddDays(-1))
                .TakeWhile(s => s <= start)
                .Any(s => s == start);
        }
        catch (EvaluationException ex)
        {
            Log.Warning(ex, $"CalendarRecurrence: Recurrence rule of calendar event {calendarEvent.Id} cannot be evaluated.");
            return false;
        }
    }

    private static IEnumerable<DateTimeOffset> Occurrences(CalendarEventEntity calendarEvent, DateTimeOffset searchStart)
    {
        CalendarEvent icalEvent = ToIcalEvent(calendarEvent);
        CalDateTime start = calendarEvent.AllDay
            ? new CalDateTime(DateOnly.FromDateTime(searchStart.UtcDateTime))
            : new CalDateTime(searchStart.UtcDateTime, CalDateTime.UtcTzId);

        foreach (Occurrence occurrence in icalEvent.GetOccurrences(start))
        {
            CalDateTime occurrenceStart = occurrence.Period.StartTime;
            yield return calendarEvent.AllDay
                ? new DateTimeOffset(occurrenceStart.Date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero)
                : new DateTimeOffset(occurrenceStart.AsUtc, TimeSpan.Zero);
        }
    }

    public static CalendarEvent ToIcalEvent(CalendarEventEntity calendarEvent)
    {
        CalendarEvent icalEvent = new()
        {
            DtStart = ToCalDateTime(calendarEvent, calendarEvent.StartAt),
            DtEnd = ToCalDateTime(calendarEvent, calendarEvent.EndAt)
        };

        if (!string.IsNullOrWhiteSpace(calendarEvent.RecurrenceRule))
            icalEvent.RecurrenceRule = new RecurrencePattern(calendarEvent.RecurrenceRule);

        return icalEvent;
    }

    public static CalDateTime ToCalDateTime(CalendarEventEntity calendarEvent, DateTimeOffset value)
    {
        if (calendarEvent.AllDay)
            return new CalDateTime(DateOnly.FromDateTime(value.UtcDateTime));

        // Recur in the event's own time zone; fall back to UTC if the zone is unknown.
        try
        {
            return new CalDateTime(value.UtcDateTime, CalDateTime.UtcTzId).ToTimeZone(calendarEvent.TimeZoneId);
        }
        catch (Exception)
        {
            return new CalDateTime(value.UtcDateTime, CalDateTime.UtcTzId);
        }
    }
}
