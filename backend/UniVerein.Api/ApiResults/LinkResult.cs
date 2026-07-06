using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults;

public class LinkResult
{
    [JsonPropertyName("link")] 
    public required string Link { get; set; }

    [JsonPropertyName("name")] 
    public required string Name { get; set; }

    [JsonPropertyName("icon")] 
    public required string Icon { get; set; }
}