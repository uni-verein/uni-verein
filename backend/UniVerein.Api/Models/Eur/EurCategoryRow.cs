namespace UniVerein.Api.Models.Eur;

public class EurCategoryRow
{
    public string Name { get; }
    public decimal Amount { get; }

    public EurCategoryRow(string name, decimal amount)
    {
        Name = name;
        Amount = amount;
    }
}
