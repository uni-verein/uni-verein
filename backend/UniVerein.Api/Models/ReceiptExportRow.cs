using System;
using System.Text.Json.Serialization;
using CsvHelper.Configuration.Attributes;
using UniVerein.Api.Converter;

namespace UniVerein.Api.Models;

public class ReceiptExportRow
{
    [JsonPropertyName("receiptDate")]
    [TypeConverter(typeof(GermanDateConverter))]
    public DateTime ReceiptDate { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("vendor")]
    public string Vendor { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("paymentMethod")]
    public string PaymentMethod { get; set; } = string.Empty;

    [JsonPropertyName("submittedBy")]
    public string SubmittedBy { get; set; } = string.Empty;

    [JsonPropertyName("fileIds")]
    public string FileIds { get; set; } = string.Empty;
}
