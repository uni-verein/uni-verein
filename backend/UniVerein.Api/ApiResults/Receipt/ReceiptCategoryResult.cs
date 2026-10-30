using System;
using System.Text.Json.Serialization;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.ApiResults.Receipt;

public class ReceiptCategoryResult
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("taxArea")]
    public TaxArea TaxArea { get; set; } = TaxArea.NON_PROFIT;
}
