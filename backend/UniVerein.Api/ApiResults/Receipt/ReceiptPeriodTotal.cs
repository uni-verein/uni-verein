using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults.Receipt;

public abstract class ReceiptPeriodTotal
{
    [JsonPropertyName("total")]
    public decimal Total { get; set; }

    [JsonPropertyName("byCategory")]
    public List<ReceiptCategoryTotal> ByCategory { get; set; } = new();
}
