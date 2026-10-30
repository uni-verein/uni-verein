using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiRequests;

public class TwoFactorDisableRequest
{
    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("password")]
    public required string Password { get; set; }

    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("code")]
    public required string Code { get; set; }
}
