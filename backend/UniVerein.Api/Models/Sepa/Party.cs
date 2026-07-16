namespace UniVerein.Api.Models.Sepa;

public class Party
{
    public string Name { get; set; } = string.Empty;
    public Address PostalAddress { get; set; } = new();
}