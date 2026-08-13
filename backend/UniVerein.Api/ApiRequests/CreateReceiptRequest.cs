using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.ApiRequests;

public class CreateReceiptRequest
{
    [Required]
    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [Required]
    [JsonPropertyName("receiptDate")]
    public DateTime ReceiptDate { get; set; }
    
    [JsonPropertyName("categoryId")]
    public Guid? CategoryId { get; set; }
    
    [JsonPropertyName("vendor")]
    public string? Vendor { get; set; }
    
    [JsonPropertyName("description")]
    public string? Description { get; set; }
    
    [JsonPropertyName("paymentMethod")]
    public ReceiptPaymentMethod? PaymentMethod { get; set; }
    
    [JsonPropertyName("files")]
    public List<IFormFile>? Files { get; set; }
}
