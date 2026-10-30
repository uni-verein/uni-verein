using System;
using System.Text.Json.Serialization;
using UniVerein.Api.Models.Enums;

namespace UniVerein.Api.ApiResults.Finance;

public class FinanceAnalyticsRowResult
{
    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("month")]
    public int Month { get; set; }

    [JsonPropertyName("kind")]
    public FinanceEntryKind Kind { get; set; }

    [JsonPropertyName("categoryId")]
    public Guid? CategoryId { get; set; }

    [JsonPropertyName("categoryName")]
    public string? CategoryName { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }
}
