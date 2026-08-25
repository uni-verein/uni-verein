using System.Text.Json.Serialization;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.ApiResults;

public class UserSettingResult
{
    [JsonPropertyName("type")]
    public UserSettingType Type { get; set; }

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }
}
