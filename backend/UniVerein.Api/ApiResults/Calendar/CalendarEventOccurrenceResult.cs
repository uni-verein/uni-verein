using System;
using System.Text.Json.Serialization;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.ApiResults.Calendar;

public class CalendarEventOccurrenceResult
{
    [JsonPropertyName("eventId")]
    public Guid EventId { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("start")]
    public DateTimeOffset Start { get; set; }

    [JsonPropertyName("end")]
    public DateTimeOffset End { get; set; }

    [JsonPropertyName("allDay")]
    public bool AllDay { get; set; }

    [JsonPropertyName("locationName")]
    public string LocationName { get; set; } = string.Empty;

    [JsonPropertyName("categoryId")]
    public Guid? CategoryId { get; set; }

    [JsonPropertyName("color")]
    public string? Color { get; set; }

    [JsonPropertyName("visibility")]
    public CalendarEventVisibility Visibility { get; set; }

    [JsonPropertyName("canEdit")]
    public bool CanEdit { get; set; }

    [JsonPropertyName("isPublic")]
    public bool IsPublic { get; set; }

    [JsonPropertyName("isRecurring")]
    public bool IsRecurring { get; set; }

    [JsonPropertyName("reminderMinutesBefore")]
    public int? ReminderMinutesBefore { get; set; }

    [JsonPropertyName("myRsvp")]
    public CalendarRsvpStatus? MyRsvp { get; set; }
}
