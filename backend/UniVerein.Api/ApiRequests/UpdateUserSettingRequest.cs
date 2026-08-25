using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiRequests;

public class UpdateUserSettingRequest
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }
}
