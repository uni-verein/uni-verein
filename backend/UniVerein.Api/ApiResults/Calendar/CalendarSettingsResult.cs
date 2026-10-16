using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults.Calendar;

public class CalendarSettingsResult
{
    [JsonPropertyName("publicCalendarEnabled")]
    public bool PublicCalendarEnabled { get; set; }

    [JsonPropertyName("publicCalendarTitle")]
    public string PublicCalendarTitle { get; set; } = string.Empty;

    [JsonPropertyName("requireKey")]
    public bool RequireKey { get; set; }

    [JsonPropertyName("publicCalendarKey")]
    public string? PublicCalendarKey { get; set; }
}
