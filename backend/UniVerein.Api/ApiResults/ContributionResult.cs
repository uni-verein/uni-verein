using System;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults;

public class ContributionResult
{
    [JsonPropertyName("id")] 
    public Guid Id { get; set; }

    [JsonPropertyName("name")] 
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("dueDate")]
    public DateTime DueDate { get; set; }

    [JsonPropertyName("paid")] 
    public bool Paid { get; set; } = false;
}