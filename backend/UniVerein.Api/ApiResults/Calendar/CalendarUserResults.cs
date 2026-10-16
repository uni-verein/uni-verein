using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults.Calendar;

public class CalendarUserResults
{
    [JsonPropertyName("items")]
    public List<CalendarUserResult> Items { get; set; } = new();

    [JsonPropertyName("total")]
    public int Total { get; set; }
}
