using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults.Income;

public class IncomeSummaryResult
{
    [JsonPropertyName("openAmount")]
    public decimal OpenAmount { get; set; }

    [JsonPropertyName("openCount")]
    public int OpenCount { get; set; }

    [JsonPropertyName("paidAmount")]
    public decimal PaidAmount { get; set; }

    [JsonPropertyName("paidCount")]
    public int PaidCount { get; set; }

    [JsonPropertyName("breakdown")]
    public List<IncomeBreakdownResult> Breakdown { get; set; } = new();
}
