using System;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults;

public class WebPageConfigResult
{
    [JsonPropertyName("id")] 
    public Guid Id { get; set; }

    [JsonPropertyName("pageName")] 
    public string PageName { get; set; } = "";

    [JsonPropertyName("logo")]
    public string Logo { get; set; } = "";
}