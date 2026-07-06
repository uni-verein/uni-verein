using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults;

public class AllContributionsResult
{
    [JsonPropertyName("items")] 
    public List<ContributionResult> Items { get; set; } = new();

    [JsonPropertyName("total")] 
    public int Total { get; set; }
}