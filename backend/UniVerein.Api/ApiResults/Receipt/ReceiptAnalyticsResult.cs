using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults.Receipt;

public class ReceiptAnalyticsResult
{
    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("byYear")]
    public List<ReceiptYearlyTotal> ByYear { get; set; } = new();

    [JsonPropertyName("byMonth")]
    public List<ReceiptMonthlyTotal> ByMonth { get; set; } = new();

    [JsonPropertyName("byCategory")]
    public List<ReceiptCategoryTotal> ByCategory { get; set; } = new();
}
