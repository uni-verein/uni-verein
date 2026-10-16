using System;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiRequests;

public class CalendarEventExceptionRequest
{
    [JsonPropertyName("occurrenceStart")]
    public DateTimeOffset OccurrenceStart { get; set; }
}
