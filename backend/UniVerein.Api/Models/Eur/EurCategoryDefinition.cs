using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.Models.Eur;

public class EurCategoryDefinition
{
    public string Name { get; }
    public TaxArea Area { get; }
    public bool IsIncome { get; }

    public EurCategoryDefinition(string name, TaxArea area, bool isIncome)
    {
        Name = name;
        Area = area;
        IsIncome = isIncome;
    }
}
