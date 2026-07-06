using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults;

public class ContributionPlanResults
{
    [JsonPropertyName("items")] 
    public List<ContributionPlanResult> Items { get; set; } = new();

    [JsonPropertyName("total")] 
    public int Total { get; set; }
}