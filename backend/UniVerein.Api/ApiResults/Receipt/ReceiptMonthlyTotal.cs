using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults.Receipt;

public class ReceiptMonthlyTotal : ReceiptPeriodTotal
{
    [JsonPropertyName("month")]
    public int Month { get; set; }
}
