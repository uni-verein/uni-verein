using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults;

public class LoginApiBlockedResult
{
    [JsonPropertyName("error")] 
    public string Error { get; set; } = string.Empty;

    [JsonPropertyName("remainingTime")] 
    public double RemainingTime { get; set; }
}