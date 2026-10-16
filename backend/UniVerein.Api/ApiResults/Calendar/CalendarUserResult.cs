using System;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults.Calendar;

public class CalendarUserResult
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("username")]
    public string Username { get; set; } = string.Empty;
}
