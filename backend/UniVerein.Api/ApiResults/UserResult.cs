using System;
using System.Text.Json.Serialization;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.ApiResults;

public class UserResult
{
    [JsonPropertyName("id")] 
    public Guid Id { get; set; }

    [JsonPropertyName("username")] 
    public string Username { get; set; } = "";

    [JsonPropertyName("email")] 
    public string Email { get; set; } = "";

    [JsonPropertyName("role")] 
    public UserRole Role { get; set; }
}