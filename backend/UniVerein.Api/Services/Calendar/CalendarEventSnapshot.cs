using System;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.Services.Calendar;

public class CalendarEventSnapshot
{
    public string Title { get; private init; } = string.Empty;
    public string Description { get; private init; } = string.Empty;
    public DateTimeOffset StartAt { get; private init; }
    public DateTimeOffset EndAt { get; private init; }
    public bool AllDay { get; private init; }
    public string LocationName { get; private init; } = string.Empty;
    public string LocationAddress { get; private init; } = string.Empty;
    public string LocationUrl { get; private init; } = string.Empty;
    public CalendarEventVisibility Visibility { get; private init; }
    public Guid? CategoryId { get; private init; }
    public string? RecurrenceRule { get; private init; }
    public string RecurrenceExceptions { get; private init; } = string.Empty;
    public bool IsPublic { get; private init; }

    public static CalendarEventSnapshot Of(CalendarEventEntity calendarEvent)
    {
        return new CalendarEventSnapshot
        {
            Title = calendarEvent.Title,
            Description = calendarEvent.Description,
            StartAt = calendarEvent.StartAt,
            EndAt = calendarEvent.EndAt,
            AllDay = calendarEvent.AllDay,
            LocationName = calendarEvent.LocationName,
            LocationAddress = calendarEvent.LocationAddress,
            LocationUrl = calendarEvent.LocationUrl,
            Visibility = calendarEvent.Visibility,
            CategoryId = calendarEvent.CategoryId,
            RecurrenceRule = calendarEvent.RecurrenceRule,
            RecurrenceExceptions = calendarEvent.RecurrenceExceptions,
            IsPublic = calendarEvent.IsPublic
        };
    }
}
