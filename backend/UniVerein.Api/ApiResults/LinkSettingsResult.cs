using System;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults;

public class LinkSettingsResult
{
    [JsonPropertyName("id")] 
    public Guid Id { get; set; }

    [JsonPropertyName("link")] 
    public string Link { get; set; } = string.Empty;

    [JsonPropertyName("name")] 
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("icon")] 
    public string Icon { get; set; } = string.Empty;
}