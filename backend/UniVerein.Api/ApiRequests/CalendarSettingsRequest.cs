using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiRequests;

public class CalendarSettingsRequest
{
    [JsonPropertyName("publicCalendarEnabled")]
    public bool PublicCalendarEnabled { get; set; }

    [MaxLength(100)]
    [JsonPropertyName("publicCalendarTitle")]
    public string? PublicCalendarTitle { get; set; }

    // Only reachable with a secret key in the link instead of fully public.
    [JsonPropertyName("requireKey")]
    public bool RequireKey { get; set; }

    // Creates a new key, so previously shared links stop working.
    [JsonPropertyName("regenerateKey")]
    public bool RegenerateKey { get; set; }
}
