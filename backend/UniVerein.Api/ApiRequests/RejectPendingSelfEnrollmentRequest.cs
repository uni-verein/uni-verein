using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiRequests;

public class RejectPendingSelfEnrollmentRequest
{
    [JsonPropertyName("reason")]
    public string? Reason { get; set; }
}
