using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiRequests;

public class LinkSettingsRequest
{
    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("link")]
    public required string Link { get; set; }

    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [Required(AllowEmptyStrings = true)]
    [JsonPropertyName("icon")]
    public string Icon { get; set; } = string.Empty;
}