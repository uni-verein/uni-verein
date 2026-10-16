using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults;

public class NotificationUnreadCountResult
{
    [JsonPropertyName("unread")]
    public int Unread { get; set; }
}
