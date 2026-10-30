using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.ApiRequests;

public class ReceiptCategoryRequest
{
    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("taxArea")]
    public TaxArea? TaxArea { get; set; }
}
