using System;
using System.Text.Json.Serialization;

namespace UniVerein.Api.ApiResults;

public class CreditorConfigResult
{
    [JsonPropertyName("id")] 
    public required Guid Id { get; set; }

    [JsonPropertyName("name")] 
    public required string Name { get; set; }

    [JsonPropertyName("iban")] 
    public required string Iban { get; set; }

    [JsonPropertyName("bic")] 
    public required string Bic { get; set; }

    [JsonPropertyName("creditorId")] 
    public required string CreditorId { get; set; }

    [JsonPropertyName("streetNameAndNumber")]
    public string? StreetNameAndNumber { get; set; }

    [JsonPropertyName("postCode")] 
    public string? PostCode { get; set; }

    [JsonPropertyName("cityName")] 
    public required string CityName { get; set; }

    [JsonPropertyName("countryCode")] 
    public required string CountryCode { get; set; }
}