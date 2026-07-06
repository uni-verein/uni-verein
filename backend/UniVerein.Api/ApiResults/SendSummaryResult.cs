using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults;

public class SendSummaryResult
{
    [JsonPropertyName("total")] 
    public int Total { get; set; }

    [JsonPropertyName("successful")] 
    public int Successful { get; set; }

    [JsonPropertyName("failed")] 
    public int Failed { get; set; }

    [JsonPropertyName("results")] 
    public List<EmailResult> Results { get; set; } = new();
}