using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiRequests;

public class ConfirmSelfEnrollmentRequest
{
    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("token")]
    public required string Token { get; set; }
}
