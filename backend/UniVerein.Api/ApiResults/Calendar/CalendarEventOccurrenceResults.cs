using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults.Calendar;

public class CalendarEventOccurrenceResults
{
    [JsonPropertyName("items")]
    public List<CalendarEventOccurrenceResult> Items { get; set; } = new();

    [JsonPropertyName("total")]
    public int Total { get; set; }
}
