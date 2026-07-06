using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiRequests;

public class MemberCountResult
{
    [JsonPropertyName("count")]
    public int Count { get; set; }
}