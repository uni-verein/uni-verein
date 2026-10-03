namespace UniVerein.Api.Models.Sepa;

public class Address
{
    public string? StreetName { get; set; }
    public string? PostCode { get; set; }
    public string City { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
}