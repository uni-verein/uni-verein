using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults;

public class RecipientResult
{
    [JsonPropertyName("email")] 
    public string Email { get; set; } = string.Empty;

    [JsonPropertyName("firstName")] 
    public string FirstName { get; set; } = string.Empty;

    [JsonPropertyName("lastName")] 
    public string LastName { get; set; } = string.Empty;
}