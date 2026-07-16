using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults;

public class FirmwareUpdateResult
{
    [JsonPropertyName("newFirmwareAvailable")] 
    public required bool NewFirmwareAvailable { get; set; }

    [JsonPropertyName("currentVersion")] 
    public string? CurrentVersion { get; set; }

    [JsonPropertyName("latestVersion")] 
    public string? LatestVersion { get; set; }
}