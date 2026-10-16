using System;
using System.Text.Json.Serialization;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.ApiResults;

public class NotificationResult
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("type")]
    public NotificationType Type { get; set; }

    [JsonPropertyName("eventId")]
    public Guid? EventId { get; set; }

    [JsonPropertyName("eventTitle")]
    public string EventTitle { get; set; } = string.Empty;

    [JsonPropertyName("eventStart")]
    public DateTimeOffset? EventStart { get; set; }

    [JsonPropertyName("actorUsername")]
    public string ActorUsername { get; set; } = string.Empty;

    [JsonPropertyName("details")]
    public string Details { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("read")]
    public bool Read { get; set; }
}
