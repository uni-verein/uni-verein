namespace UniVerein.Api.Models.Sepa;

public class Address
{
    public string City { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string? AddressLine { get; set; }
}