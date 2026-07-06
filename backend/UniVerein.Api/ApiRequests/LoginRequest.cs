using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiRequests;

public class LoginRequest
{
    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("username")]
    public required string Username { get; set; }

    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("password")]
    public required string Password { get; set; }
}