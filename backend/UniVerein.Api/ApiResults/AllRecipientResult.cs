using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults;

public class AllRecipientResult
{
    [JsonPropertyName("items")] 
    public List<RecipientResult> Items { get; set; } = new();

    [JsonPropertyName("total")] 
    public int Total { get; set; }
}