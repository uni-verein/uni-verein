using System.Collections.Generic;
using System.Linq;

namespace UniVerein.Api.Models.Eur;

public class EurReport
{
    public required int Year { get; init; }
    public required string ClubName { get; init; }
    public string? ClubAddress { get; init; }
    public string? TaxNumber { get; init; }
    public string? Treasurer { get; init; }
    public required List<EurAreaBlock> Areas { get; init; }
    public required List<EurTaxCheck> TaxChecks { get; init; }
    public required string TaxLimitsNote { get; init; }
    public required List<EurJournalLine> Journal { get; init; }
    public required List<EurContributionProof> ContributionProofs { get; init; }
    public required List<EurCategoryDefinition> Categories { get; init; }

    public decimal TotalIncome => Areas.Sum(x => x.TotalIncome);
    public decimal TotalExpenses => Areas.Sum(x => x.TotalExpenses);
    public int JournalErrors => Journal.Count(x => x.Check != "OK");
}
