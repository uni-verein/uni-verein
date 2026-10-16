using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults.Calendar;

public class PublicCalendarOccurrenceResults
{
    [JsonPropertyName("items")]
    public List<PublicCalendarOccurrenceResult> Items { get; set; } = new();

    [JsonPropertyName("total")]
    public int Total { get; set; }
}
