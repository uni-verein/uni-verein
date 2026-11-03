using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiRequests;

public class SetupRequest
{
    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("token")]
    public required string Token { get; set; }

    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("username")]
    public required string Username { get; set; }

    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("email")]
    public required string Email { get; set; }

    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("password")]
    public required string Password { get; set; }
}
