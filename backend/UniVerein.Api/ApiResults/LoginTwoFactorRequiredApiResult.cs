using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults;

public class LoginTwoFactorRequiredApiResult
{
    [JsonPropertyName("requiresTwoFactor")]
    public bool RequiresTwoFactor { get; set; } = true;

    [JsonPropertyName("challenge")]
    public string Challenge { get; set; } = string.Empty;
}
