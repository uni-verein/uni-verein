using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiRequests;

public class MemberCategoryUpdateRequest
{
    [Required] 
    [JsonPropertyName("name")] 
    public required string Name { get; set; }

    [Required]
    [JsonPropertyName("category")]
    public required string Category { get; set; }
}