using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults;

public class TwoFactorEnabledApiResult
{
    [JsonPropertyName("recoveryCodes")]
    public List<string> RecoveryCodes { get; set; } = new();
}
