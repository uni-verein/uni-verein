using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults;

public class AuditLogResults
{
    [JsonPropertyName("items")] 
    public List<AuditLogResult> Items { get; set; } = new();

    [JsonPropertyName("total")] 
    public int Total { get; set; }
}