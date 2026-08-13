using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults.Receipt;

public class AllReceiptResults
{
    [JsonPropertyName("items")]
    public List<ReceiptResult> Items { get; set; } = new();

    [JsonPropertyName("total")]
    public int Total { get; set; }
}
