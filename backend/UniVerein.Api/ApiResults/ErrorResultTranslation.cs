using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults;

public class ErrorResultTranslation
{
    [JsonPropertyName("translationKey")] 
    public string TranslationKey { get; set; } = string.Empty;

    [JsonPropertyName("values")] 
    public List<string> Values { get; set; } = new();
}