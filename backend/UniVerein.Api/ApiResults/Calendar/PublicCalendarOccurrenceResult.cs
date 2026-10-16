using System;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults.Calendar;

// Only what may be shown to the public: no participants, owner or other personal data.
public class PublicCalendarOccurrenceResult
{
    [JsonPropertyName("eventId")]
    public Guid EventId { get; set; }

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

    [JsonPropertyName("categoryId")]
    public Guid? CategoryId { get; set; }

    [JsonPropertyName("categoryName")]
    public string? CategoryName { get; set; }

    [JsonPropertyName("color")]
    public string? Color { get; set; }
}
