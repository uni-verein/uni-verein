using System;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults;

public class AuthSessionApiResult
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;
}
