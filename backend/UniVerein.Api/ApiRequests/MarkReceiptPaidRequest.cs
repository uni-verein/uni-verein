using System.Text.Json.Serialization;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.ApiRequests;

public class MarkReceiptPaidRequest
{
    [JsonPropertyName("paymentMethod")]
    public ReceiptPaymentMethod? PaymentMethod { get; set; }
}
