using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiRequests;

public class LinkSettingsUpdateRequest
{
    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("link")]
    public string? Link { get; set; }

    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [Required(AllowEmptyStrings = true)]
    [JsonPropertyName("icon")]
    public string Icon { get; set; } = string.Empty;
}