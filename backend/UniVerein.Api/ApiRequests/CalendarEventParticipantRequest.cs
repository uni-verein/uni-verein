using System;
using System.Text.Json.Serialization;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.ApiRequests;

public class CalendarEventParticipantRequest
{
    [JsonPropertyName("userId")]
    public Guid UserId { get; set; }

    [JsonPropertyName("role")]
    public CalendarParticipantRole Role { get; set; } = CalendarParticipantRole.ATTENDEE;
}
