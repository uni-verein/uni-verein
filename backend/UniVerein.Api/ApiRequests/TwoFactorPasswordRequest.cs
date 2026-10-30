using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiRequests;

public class TwoFactorPasswordRequest
{
    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("password")]
    public required string Password { get; set; }
}
