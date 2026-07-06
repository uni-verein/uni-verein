using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults;

public class SidebarResult
{
    [JsonPropertyName("showSepa")] 
    public required bool ShowSepa { get; set; }

    [JsonPropertyName("showMail")]
    public required bool ShowMail { get; set; }

    [JsonPropertyName("links")] 
    public List<LinkResult> Links { get; set; } = new();
}