using System;
using System.Text.Json.Serialization;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.ApiResults.Calendar;

public class CalendarEventParticipantResult
{
    [JsonPropertyName("userId")]
    public Guid UserId { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;

    [JsonPropertyName("role")]
    public CalendarParticipantRole Role { get; set; }

    [JsonPropertyName("rsvpStatus")]
    public CalendarRsvpStatus RsvpStatus { get; set; }
}
