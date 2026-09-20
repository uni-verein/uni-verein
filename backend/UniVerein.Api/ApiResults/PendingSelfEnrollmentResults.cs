using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults;

public class PendingSelfEnrollmentResults
{
    [JsonPropertyName("items")]
    public List<PendingSelfEnrollmentResult> Items { get; set; } = new();

    [JsonPropertyName("total")]
    public int Total { get; set; }
}
