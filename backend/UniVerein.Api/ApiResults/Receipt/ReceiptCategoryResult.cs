using System;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults.Receipt;

public class ReceiptCategoryResult
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
