namespace UniVerein.Api.Models;

public class ClubIdentity
{
    public string Name { get; }
    public string? Address { get; }
    public string? TaxNumber { get; }

    public ClubIdentity(string name, string? address, string? taxNumber)
    {
        Name = name;
        Address = address;
        TaxNumber = taxNumber;
    }
}
