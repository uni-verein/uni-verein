using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.ApiResults.Calendar;

public class CalendarEventResult
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("start")]
    public DateTimeOffset Start { get; set; }

    [JsonPropertyName("end")]
    public DateTimeOffset End { get; set; }

    [JsonPropertyName("allDay")]
    public bool AllDay { get; set; }

    [JsonPropertyName("locationName")]
    public string LocationName { get; set; } = string.Empty;

    [JsonPropertyName("locationAddress")]
    public string LocationAddress { get; set; } = string.Empty;

    [JsonPropertyName("locationUrl")]
    public string LocationUrl { get; set; } = string.Empty;

    [JsonPropertyName("visibility")]
    public CalendarEventVisibility Visibility { get; set; }

    [JsonPropertyName("isPublic")]
    public bool IsPublic { get; set; }

    [JsonPropertyName("isPubliclyVisible")]
    public bool IsPubliclyVisible { get; set; }

    [JsonPropertyName("recurrenceRule")]
    public string? RecurrenceRule { get; set; }

    [JsonPropertyName("recurrenceExceptions")]
    public List<DateTimeOffset> RecurrenceExceptions { get; set; } = new();

    [JsonPropertyName("reminderMinutesBefore")]
    public int? ReminderMinutesBefore { get; set; }

    [JsonPropertyName("category")]
    public CalendarCategoryResult? Category { get; set; }

    [JsonPropertyName("owner")]
    public CalendarUserResult Owner { get; set; } = new();

    [JsonPropertyName("participants")]
    public List<CalendarEventParticipantResult> Participants { get; set; } = new();

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset UpdatedAt { get; set; }

    [JsonPropertyName("lastModifiedBy")]
    public CalendarUserResult? LastModifiedBy { get; set; }

    [JsonPropertyName("canEdit")]
    public bool CanEdit { get; set; }

    [JsonPropertyName("canDelete")]
    public bool CanDelete { get; set; }

    [JsonPropertyName("isSubscribed")]
    public bool IsSubscribed { get; set; }

    [JsonPropertyName("myRsvp")]
    public CalendarRsvpStatus? MyRsvp { get; set; }
}
