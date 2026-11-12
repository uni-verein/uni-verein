using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults;

public class EnrollLegalTextResult
{
    [JsonPropertyName("legalText")]
    public string LegalText { get; set; } = string.Empty;
}
