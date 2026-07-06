using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.ApiRequests;

public class UserRequest
{
    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("username")]
    public required string Username { get; set; }

    [Required(AllowEmptyStrings = true)]
    [JsonPropertyName("email")]
    public string Email { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("password")]
    public required string Password { get; set; }

    [Required] [JsonPropertyName("role")] 
    public required UserRole Role { get; set; }
}