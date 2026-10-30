using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults.Income;

public class IncomeYearsResult
{
    [JsonPropertyName("years")]
    public List<int> Years { get; set; } = new();
}
