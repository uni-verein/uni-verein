using System.Text.Json.Serialization;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.ApiRequests;

public class CalendarRsvpRequest
{
    [JsonPropertyName("status")]
    public CalendarRsvpStatus Status { get; set; }
}
