using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults;

public class TwoFactorSetupApiResult
{
    [JsonPropertyName("secret")]
    public string Secret { get; set; } = string.Empty;

    [JsonPropertyName("otpAuthUri")]
    public string OtpAuthUri { get; set; } = string.Empty;
}
