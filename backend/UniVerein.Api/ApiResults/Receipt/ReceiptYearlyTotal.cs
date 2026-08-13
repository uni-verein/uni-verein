using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults.Receipt;

public class ReceiptYearlyTotal : ReceiptPeriodTotal
{
    [JsonPropertyName("year")]
    public int Year { get; set; }
}
