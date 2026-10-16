using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults.Calendar;

public class CalendarCategoryResults
{
    [JsonPropertyName("items")]
    public List<CalendarCategoryResult> Items { get; set; } = new();

    [JsonPropertyName("total")]
    public int Total { get; set; }
}
