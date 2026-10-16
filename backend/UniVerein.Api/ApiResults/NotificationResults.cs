using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults;

public class NotificationResults
{
    [JsonPropertyName("items")]
    public List<NotificationResult> Items { get; set; } = new();

    [JsonPropertyName("total")]
    public int Total { get; set; }

    [JsonPropertyName("unread")]
    public int Unread { get; set; }
}
