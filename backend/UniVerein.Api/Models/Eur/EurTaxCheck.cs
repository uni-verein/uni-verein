namespace UniVerein.Api.Models.Eur;

public class EurTaxCheck
{
    public string Label { get; }
    public decimal Value { get; }
    public decimal Limit { get; }
    public string Status { get; }
    public bool Ok { get; }

    public EurTaxCheck(string label, decimal value, decimal limit, string status, bool ok)
    {
        Label = label;
        Value = value;
        Limit = limit;
        Status = status;
        Ok = ok;
    }
}
