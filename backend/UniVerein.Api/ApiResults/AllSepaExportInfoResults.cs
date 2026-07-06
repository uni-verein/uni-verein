using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults;

public class AllSepaExportInfoResults
{
    [JsonPropertyName("items")] 
    public List<SepaExportInfoResult> Items { get; set; } = new();

    [JsonPropertyName("total")] 
    public int Total { get; set; }
}