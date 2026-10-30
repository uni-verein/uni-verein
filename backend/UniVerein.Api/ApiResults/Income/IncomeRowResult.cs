using System;
using System.Text.Json.Serialization;
using UniVerein.Api.Models.Enums;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.ApiResults.Income;

public class IncomeRowResult
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("source")]
    public IncomeSource Source { get; set; }

    [JsonPropertyName("kind")]
    public IncomeKind Kind { get; set; }

    [JsonPropertyName("categoryId")]
    public Guid? CategoryId { get; set; }

    [JsonPropertyName("categoryName")]
    public string? CategoryName { get; set; }

    [JsonPropertyName("payer")]
    public string Payer { get; set; } = string.Empty;

    [JsonPropertyName("memberNumber")]
    public int? MemberNumber { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("dueDate")]
    public DateTime DueDate { get; set; }

    [JsonPropertyName("paid")]
    public bool Paid { get; set; }

    [JsonPropertyName("membershipStatus")]
    public MembershipStatus? MembershipStatus { get; set; }

    [JsonPropertyName("acknowledgementNumber")]
    public string? AcknowledgementNumber { get; set; }

    [JsonPropertyName("bookingNumber")]
    public string? BookingNumber { get; set; }

    [JsonPropertyName("paymentDate")]
    public DateTime? PaymentDate { get; set; }

    [JsonPropertyName("deletedAt")]
    public DateTimeOffset? DeletedAt { get; set; }
}
