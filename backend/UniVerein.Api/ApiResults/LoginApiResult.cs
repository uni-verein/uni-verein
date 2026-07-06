using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults;

public class LoginApiResult
{
    [JsonPropertyName("token")] 
    public string Token { get; set; } = string.Empty;
}