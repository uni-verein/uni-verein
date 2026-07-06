using System;
using System.Text.Json.Serialization;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.ApiResults;

public class ContributionPlanResult
{
    [JsonPropertyName("id")] 
    public Guid Id { get; set; }

    [JsonPropertyName("name")] 
    public string Name { get; set; } = "";

    [JsonPropertyName("amount")] 
    public decimal Amount { get; set; }

    [JsonPropertyName("interval")]
    public Interval Interval { get; set; } = Interval.MONTHLY;
}