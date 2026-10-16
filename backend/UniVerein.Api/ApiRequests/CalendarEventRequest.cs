using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.ApiRequests;

public class CalendarEventRequest
{
    [Required(AllowEmptyStrings = false)]
    [MaxLength(200)]
    [JsonPropertyName("title")]
    public required string Title { get; set; }

    [MaxLength(10000)]
    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("start")]
    public DateTimeOffset Start { get; set; }

    [JsonPropertyName("end")]
    public DateTimeOffset End { get; set; }

    [JsonPropertyName("allDay")]
    public bool AllDay { get; set; }

    [MaxLength(200)]
    [JsonPropertyName("locationName")]
    public string? LocationName { get; set; }

    [MaxLength(500)]
    [JsonPropertyName("locationAddress")]
    public string? LocationAddress { get; set; }

    [MaxLength(1000)]
    [JsonPropertyName("locationUrl")]
    public string? LocationUrl { get; set; }

    [EnumDataType(typeof(CalendarEventVisibility))]
    [JsonPropertyName("visibility")]
    public CalendarEventVisibility Visibility { get; set; } = CalendarEventVisibility.CLUB;

    [JsonPropertyName("categoryId")]
    public Guid? CategoryId { get; set; }

    [MaxLength(500)]
    [JsonPropertyName("recurrenceRule")]
    public string? RecurrenceRule { get; set; }

    [Range(0, 10080)]
    [JsonPropertyName("reminderMinutesBefore")]
    public int? ReminderMinutesBefore { get; set; }

    [JsonPropertyName("isPublic")]
    public bool IsPublic { get; set; }

    [JsonPropertyName("participants")]
    public List<CalendarEventParticipantRequest> Participants { get; set; } = new();

    [JsonPropertyName("version")]
    public int? Version { get; set; }
}
