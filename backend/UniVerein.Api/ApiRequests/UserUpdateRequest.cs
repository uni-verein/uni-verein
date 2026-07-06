using System.Text.Json.Serialization;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.ApiRequests;

public class UserUpdateRequest
{
    [JsonPropertyName("username")] 
    public string? Username { get; set; }

    [JsonPropertyName("password")] 
    public string? Password { get; set; }

    [JsonPropertyName("email")] 
    public string? Email { get; set; }

    [JsonPropertyName("role")] 
    public UserRole? Role { get; set; }
}