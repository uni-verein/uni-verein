namespace UniVerein.Api.Models;

public class CreditorConfig
{
    public required string Name { get; init; }
    public required string Iban { get; init; }
    public required string Bic { get; init; }
    public required string CreditorId { get; init; }
    public string? StreetName { get; init; }
    public string? PostCode { get; init; }
    public required string TownName { get; init; }
    public required string Country { get; init; }
}