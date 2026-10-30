using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiRequests;

public class IncomeUpdateRequest
{
    [JsonPropertyName("categoryId")]
    public Guid CategoryId { get; set; }

    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("payer")]
    public required string Payer { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("incomeDate")]
    public DateTime IncomeDate { get; set; }
}
