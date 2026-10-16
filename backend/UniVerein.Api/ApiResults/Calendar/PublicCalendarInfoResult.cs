using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults.Calendar;

public class PublicCalendarInfoResult
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("categories")]
    public List<CalendarCategoryResult> Categories { get; set; } = new();
}
