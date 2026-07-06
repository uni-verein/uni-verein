using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults;

public class EmailResult
{
    [JsonPropertyName("email")] 
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("success")] 
    public bool Success { get; set; }

    [JsonPropertyName("errorMessage")] 
    public string? ErrorMessage { get; set; }
}