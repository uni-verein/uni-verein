using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiRequests;

public class ReceiptCategoryRequest
{
    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("name")]
    public required string Name { get; set; }
}
