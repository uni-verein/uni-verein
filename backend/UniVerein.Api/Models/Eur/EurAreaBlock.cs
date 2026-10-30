using System.Collections.Generic;
using System.Linq;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.Models.Eur;

public class EurAreaBlock
{
    public required TaxArea Area { get; init; }
    public required List<EurCategoryRow> Income { get; init; }
    public required List<EurCategoryRow> Expenses { get; init; }

    public decimal TotalIncome => Income.Sum(x => x.Amount);
    public decimal TotalExpenses => Expenses.Sum(x => x.Amount);
    public decimal Result => TotalIncome - TotalExpenses;
}
