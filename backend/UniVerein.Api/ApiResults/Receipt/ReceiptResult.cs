using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.ApiResults.Receipt;

public class ReceiptResult
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("userId")]
    public Guid UserId { get; set; }

    [JsonPropertyName("userName")]
    public string? UserName { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("receiptDate")]
    public DateTime ReceiptDate { get; set; }

    [JsonPropertyName("categoryId")]
    public Guid? CategoryId { get; set; }

    [JsonPropertyName("categoryName")]
    public string? CategoryName { get; set; }

    [JsonPropertyName("vendor")]
    public string? Vendor { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("paymentMethod")]
    public ReceiptPaymentMethod? PaymentMethod { get; set; }

    [JsonPropertyName("paid")]
    public bool Paid { get; set; }

    [JsonPropertyName("files")]
    public List<ReceiptFileResult> Files { get; set; } = new();

    [JsonPropertyName("createdAt")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("deletedAt")]
    public DateTimeOffset? DeletedAt { get; set; }
}
