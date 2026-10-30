using System;
using System.Text.Json.Serialization;
using UniVerein.Api.Models.Enums;

namespace UniVerein.Api.ApiRequests;

public class AcknowledgementRequest
{
    [JsonPropertyName("source")]
    public IncomeSource Source { get; set; }

    [JsonPropertyName("id")]
    public Guid Id { get; set; }
}
