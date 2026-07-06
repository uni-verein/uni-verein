using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults;

public class ErrorDetailsResult
{
    [Required]
    [JsonPropertyName("errorMessage")]
    public string ErrorMessage { get; set; } = null!;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [JsonPropertyName("moreInfo")]
    public string? MoreInfo { get; set; }

    [Required]
    [JsonPropertyName("statusCode")]
    public int StatusCode { get; set; }

    [Required]
    [JsonPropertyName("errorCode")]
    public string ErrorCode { get; set; } = null!;

    [JsonPropertyName("errorResultTranslations")]
    public List<ErrorResultTranslation> ErrorResultTranslation { get; set; } = new();

    public override string ToString()
    {
        return JsonSerializer.Serialize(this);
    }
}