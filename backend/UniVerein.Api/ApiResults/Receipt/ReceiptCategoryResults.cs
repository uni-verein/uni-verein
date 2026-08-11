using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults.Receipt;

public class ReceiptCategoryResults
{
    [JsonPropertyName("items")]
    public List<ReceiptCategoryResult> Items { get; set; } = new();

    [JsonPropertyName("total")]
    public int Total { get; set; }
}
