using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults;

public class SetupStatusResult
{
    [JsonPropertyName("needsSetup")]
    public bool NeedsSetup { get; set; }
}
