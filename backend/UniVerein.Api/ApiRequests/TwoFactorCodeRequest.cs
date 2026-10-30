using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiRequests;

public class TwoFactorCodeRequest
{
    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("code")]
    public required string Code { get; set; }
}
