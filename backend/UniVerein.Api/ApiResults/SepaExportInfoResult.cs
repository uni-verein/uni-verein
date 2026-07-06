using System;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults;

public class SepaExportInfoResult
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }
    
    [JsonPropertyName("name")] 
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public required decimal Amount { get; set; }

    [JsonPropertyName("exportedCases")] 
    public required int ExportedCases { get; set; }

    [JsonPropertyName("exportedDate")]
    public required DateTimeOffset ExportedDate { get; set; }
}