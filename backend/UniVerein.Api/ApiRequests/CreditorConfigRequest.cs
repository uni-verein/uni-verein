using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiRequests;

public class CreditorConfigRequest
{
    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("iban")]
    public required string Iban { get; set; }

    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("bic")]
    public required string Bic { get; set; }

    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("creditorId")]
    public required string CreditorId { get; set; }

    [JsonPropertyName("streetNameAndNumber")]
    public string? StreetNameAndNumber { get; set; }

    [JsonPropertyName("postCode")] public string? PostCode { get; set; }

    [Required(AllowEmptyStrings = false)]
    [JsonPropertyName("cityName")]
    public required string CityName { get; set; }

    [Required]
    [JsonPropertyName("countryCode")]
    public required string CountryCode { get; set; }
}