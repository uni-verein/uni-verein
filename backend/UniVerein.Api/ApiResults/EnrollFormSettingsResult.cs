using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults;

public class EnrollFormSettingsResult
{
    [JsonPropertyName("selfEnrollmentEnabled")]
    public bool SelfEnrollmentEnabled { get; set; }

    [JsonPropertyName("legalText")]
    public string LegalText { get; set; } = string.Empty;

    [JsonPropertyName("embedAllowedOrigins")]
    public string EmbedAllowedOrigins { get; set; } = string.Empty;
}
