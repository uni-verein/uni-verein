using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiRequests;

public class TwoFactorLoginRequest
{
    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("challenge")]
    public required string Challenge { get; set; }

    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("code")]
    public required string Code { get; set; }
}
