using System.Text.Json.Serialization;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.ApiRequests;

public class ContributionPlanUpdateRequest
{
    [JsonPropertyName("name")] 
    public string? Name { get; set; }

    [JsonPropertyName("amount")] 
    public decimal? Amount { get; set; }

    [JsonPropertyName("interval")] 
    public Interval? Interval { get; set; }
}