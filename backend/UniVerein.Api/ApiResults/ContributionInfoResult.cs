using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults;

public class ContributionInfoResult
{
    [JsonPropertyName("openPayments")] 
    public int OpenPayments { get; set; }

    [JsonPropertyName("openAmount")] 
    public decimal OpenAmount { get; set; }
}