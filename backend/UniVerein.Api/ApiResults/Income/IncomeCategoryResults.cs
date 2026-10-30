using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults.Income;

public class IncomeCategoryResults
{
    [JsonPropertyName("items")]
    public List<IncomeCategoryResult> Items { get; set; } = new();

    [JsonPropertyName("total")]
    public int Total { get; set; }
}
