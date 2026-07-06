using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults;

public class UserResults
{
    [JsonPropertyName("items")]
    public List<UserResult> Items { get; set; } = new();

    [JsonPropertyName("total")]
    public int Total { get; set; }
}