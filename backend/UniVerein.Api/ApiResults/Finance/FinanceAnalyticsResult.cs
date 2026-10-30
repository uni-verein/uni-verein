using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults.Finance;

public class FinanceAnalyticsResult
{
    [JsonPropertyName("rows")]
    public List<FinanceAnalyticsRowResult> Rows { get; set; } = new();
}
