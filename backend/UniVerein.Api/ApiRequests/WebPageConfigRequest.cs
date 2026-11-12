using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiRequests;

public class WebPageConfigRequest
{
    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("pageName")]
    public required string PageName { get; set; }

    [Required(AllowEmptyStrings = true)]
    [JsonPropertyName("logo")]
    public string Logo { get; set; } = string.Empty;
}