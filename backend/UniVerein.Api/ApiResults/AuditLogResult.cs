using System;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults;

public class AuditLogResult
{
    [JsonPropertyName("timestamp")] 
    public DateTimeOffset Timestamp { get; set; }

    [JsonPropertyName("userName")] 
    public string UserName { get; set; } = string.Empty;

    [JsonPropertyName("action")] 
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("entity")] 
    public string Entity { get; set; } = string.Empty;

    [JsonPropertyName("data")]
    public string Data { get; set; } = string.Empty;
}