using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults;

public class AllLinkSettingsResults
{
    [JsonPropertyName("items")] 
    public List<LinkSettingsResult> Items { get; set; } = new();

    [JsonPropertyName("total")] 
    public int Total { get; set; }
}