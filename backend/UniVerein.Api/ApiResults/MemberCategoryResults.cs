using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults;

public class MemberCategoryResults
{
    [JsonPropertyName("items")] 
    public List<MemberCategoryResult> Items { get; set; } = new();

    [JsonPropertyName("total")] 
    public int Total { get; set; }
}