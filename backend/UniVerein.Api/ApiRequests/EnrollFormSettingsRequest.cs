using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiRequests;

public class EnrollFormSettingsRequest
{
    [JsonPropertyName("selfEnrollmentEnabled")]
    public bool SelfEnrollmentEnabled { get; set; }

    [MaxLength(EnrollFormSettingsLimits.LegalTextMaxLength)]
    [JsonPropertyName("legalText")]
    public string LegalText { get; set; } = string.Empty;

    [MaxLength(EnrollFormSettingsLimits.EmbedAllowedOriginsMaxLength)]
    [JsonPropertyName("embedAllowedOrigins")]
    public string EmbedAllowedOrigins { get; set; } = string.Empty;
}
