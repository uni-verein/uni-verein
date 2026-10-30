using System;
using System.Text.Json.Serialization;
using UniVerein.Api.Models.Enums;

namespace UniVerein.Api.ApiResults.Income;

public class IncomeBreakdownResult
{
    [JsonPropertyName("kind")]
    public IncomeKind Kind { get; set; }

    [JsonPropertyName("categoryId")]
    public Guid? CategoryId { get; set; }

    [JsonPropertyName("categoryName")]
    public string? CategoryName { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }
}
