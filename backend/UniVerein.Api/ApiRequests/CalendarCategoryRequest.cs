using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiRequests;

public class CalendarCategoryRequest
{
    [Required(AllowEmptyStrings = false)]
    [MaxLength(50)]
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [Required(AllowEmptyStrings = false)]
    [RegularExpression("^#[0-9a-fA-F]{6}$")]
    [JsonPropertyName("color")]
    public required string Color { get; set; }

    [JsonPropertyName("isPublic")]
    public bool IsPublic { get; set; }
}
