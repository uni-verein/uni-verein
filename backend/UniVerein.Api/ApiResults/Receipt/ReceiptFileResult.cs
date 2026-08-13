using System;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults.Receipt;

public class ReceiptFileResult
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("contentType")]
    public string ContentType { get; set; } = string.Empty;

    [JsonPropertyName("position")]
    public int Position { get; set; }
}
