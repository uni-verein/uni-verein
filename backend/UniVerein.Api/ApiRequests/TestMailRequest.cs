using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiRequests;

public class TestMailRequest
{
    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("email")]
    public required string Email { get; set; }
}