namespace UniVerein.Api.Data.Sepa;

public class Party
{
    public string Name { get; set; } = string.Empty;
    public Address PostalAddress { get; set; } = new();
}