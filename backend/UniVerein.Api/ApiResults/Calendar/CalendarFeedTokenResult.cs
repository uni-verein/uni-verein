using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults.Calendar;

public class CalendarFeedTokenResult
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;

    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;
}
