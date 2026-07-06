using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults;

public class AllMemberResults
{
    [JsonPropertyName("items")] 
    public List<MemberResult> Items { get; set; } = new();

    [JsonPropertyName("total")] 
    public int Total { get; set; }
}