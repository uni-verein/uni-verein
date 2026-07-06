using System;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults;

public class MemberCategoryResult
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("category")] 
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("name")] 
    public string Name { get; set; } = string.Empty;
}