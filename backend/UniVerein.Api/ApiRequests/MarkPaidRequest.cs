using System;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiRequests;

public class MarkPaidRequest
{
    [JsonPropertyName("paid")]
    public bool Paid { get; set; }

    [JsonPropertyName("paymentDate")]
    public DateTime? PaymentDate { get; set; }
}
