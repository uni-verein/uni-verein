using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults;

public class SelfEnrollmentSubmitResult
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = "Confirmation email sent.";
}
