using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using UniVerein.Api.Models;
using UniVerein.Api.Models.Eur;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;

namespace UniVerein.Api.Services;

public class EurExportService
{
    private const int RaisedLimitsFromYear = 2026;
    private const decimal LegacyLimit = 45000m;
    private const decimal RaisedAllowance = 50000m;
    private const decimal RaisedTimelyUseThreshold = 100000m;

    private readonly AppDbContext _db;
    private readonly ClubIdentityService _clubIdentityService;
    private readonly TimeProvider _timeProvider;
    private readonly BookingNumberService _bookingNumberService;

    public EurExportService(AppDbContext db, ClubIdentityService clubIdentityService, TimeProvider timeProvider,
        BookingNumberService bookingNumberService)
    {
        _db = db;
        _clubIdentityService = clubIdentityService;
        _timeProvider = timeProvider;
        _bookingNumberService = bookingNumberService;
    }

    public async Task<EurReport> BuildAsync(int year)
    {
        List<IncomeCategoryEntity> incomeCategories = await _db.IncomeCategories.ToListAsync();
        List<ReceiptCategoryEntity> receiptCategories = await _db.ReceiptCategories.ToListAsync();

        List<EurPendingLine> lines = new();
        (List<EurPendingLine> contributionLines, List<EurContributionProof> contributionProofs) = await LoadContributionLinesAsync(year);
        lines.AddRange(contributionLines);
        lines.AddRange(await LoadIncomeLinesAsync(year));
        lines.AddRange(await LoadReceiptLinesAsync(year));

        List<EurJournalLine> journal = lines
            .OrderBy(x => x.Date)
            .ThenBy(x => x.Order)
            .ThenBy(x => x.Number, StringComparer.Ordinal)
            .ThenBy(x => x.Text, StringComparer.CurrentCulture)
            .Select(x => new EurJournalLine(x.Number, x.Date, x.Text, x.Category, x.Area, x.IsIncome, x.Account, x.Amount))
            .ToList();

        List<EurCategoryDefinition> categories = new() { new(EurLabels.Membership, TaxArea.NON_PROFIT, true) };
        categories.AddRange(incomeCategories.Select(x => new EurCategoryDefinition(x.Name, x.TaxArea, true)));
        categories.AddRange(receiptCategories.Select(x => new EurCategoryDefinition(x.Name, x.TaxArea, false)));

        List<EurAreaBlock> areas = Enum.GetValues<TaxArea>().Select(area => BuildBlock(area, categories, journal)).ToList();
        ClubIdentity club = await _clubIdentityService.GetAsync();

        return new EurReport
        {
            Year = year,
            ClubName = club.Name,
            ClubAddress = club.Address,
            TaxNumber = club.TaxNumber,
            Treasurer = await FindTreasurerAsync(),
            Areas = areas,
            TaxChecks = BuildTaxChecks(areas, year),
            TaxLimitsNote = TaxLimitsNote(year),
            Journal = journal,
            ContributionProofs = contributionProofs,
            Categories = categories
                .OrderBy(x => x.Area)
                .ThenByDescending(x => x.IsIncome)
                .ThenBy(x => x.Name == EurLabels.Membership ? 0 : 1)
                .ThenBy(x => x.Name, StringComparer.CurrentCulture)
                .ToList()
        };
    }

    private static EurAreaBlock BuildBlock(TaxArea area, List<EurCategoryDefinition> categories, List<EurJournalLine> journal)
    {
        return new EurAreaBlock
        {
            Area = area,
            Income = BuildRows(area, true, categories, journal),
            Expenses = BuildRows(area, false, categories, journal)
        };
    }

    private static List<EurCategoryRow> BuildRows(TaxArea area, bool isIncome, List<EurCategoryDefinition> categories, List<EurJournalLine> journal)
    {
        List<EurJournalLine> areaLines = journal.Where(x => x.Area == area && x.IsIncome == isIncome).ToList();
        IEnumerable<string> names = categories
            .Where(x => x.Area == area && x.IsIncome == isIncome)
            .Select(x => x.Name)
            .Concat(areaLines.Select(x => x.Category))
            .Distinct();

        return names
            .OrderBy(x => x == EurLabels.Membership ? 0 : 1)
            .ThenBy(x => x, StringComparer.CurrentCulture)
            .Select(name => new EurCategoryRow(name, areaLines.Where(x => x.Category == name).Sum(x => x.Amount)))
            .ToList();
    }

    private static string TaxLimitsNote(int year)
    {
        return year >= RaisedLimitsFromYear
            ? "Grenzwerte gemäß Steueränderungsgesetz 2025, gültig ab 01.01.2026."
            : $"Grenzwerte für das Geschäftsjahr {year} (vor dem Steueränderungsgesetz 2025): 45.000 €.";
    }

    private static List<EurTaxCheck> BuildTaxChecks(List<EurAreaBlock> areas, int year)
    {
        bool raised = year >= RaisedLimitsFromYear;
        decimal wgbAllowance = raised ? RaisedAllowance : LegacyLimit;
        decimal zweckbetriebAllowance = raised ? RaisedAllowance : LegacyLimit;
        decimal timelyUseThreshold = raised ? RaisedTimelyUseThreshold : LegacyLimit;

        decimal wgb = areas.Single(x => x.Area == TaxArea.COMMERCIAL_OPERATION).TotalIncome;
        decimal zweckbetrieb = areas.Single(x => x.Area == TaxArea.PURPOSE_RELATED_OPERATION).TotalIncome;
        decimal total = areas.Sum(x => x.TotalIncome);

        return new List<EurTaxCheck>
        {
            Check("Einnahmen wirtschaftl. Geschäftsbetrieb (brutto) – Freigrenze § 64 Abs. 3 AO", wgb, wgbAllowance,
                "✔ KSt/GewSt frei", "✖ Freigrenze überschritten!"),
            Check("Einnahmen Zweckbetrieb + WGB – Aufteilung entbehrlich unter", wgb + zweckbetrieb, zweckbetriebAllowance,
                "✔ Keine Abgrenzung nötig", "✖ Abgrenzung Z/WGB nötig"),
            Check("Gesamteinnahmen – zeitnahe Mittelverwendung erst ab", total, timelyUseThreshold,
                "✔ Keine Pflicht", "✖ Mittelverwendung beachten")
        };
    }

    private static EurTaxCheck Check(string label, decimal value, decimal limit, string okStatus, string failStatus)
    {
        bool ok = value <= limit;
        return new EurTaxCheck(label, value, limit, ok ? okStatus : failStatus, ok);
    }

    private async Task<(List<EurPendingLine> Lines, List<EurContributionProof> Proofs)> LoadContributionLinesAsync(int year)
    {
        var contributions = await _db.Contributions
            .Where(x => x.DeletedAt == null && x.Paid != null && x.PaymentDate != null && x.PaymentDate.Value.Year == year)
            .Select(x => new { x.Id, x.DueDate, PaymentDate = x.PaymentDate!.Value, x.Amount, x.MemberEntity.MemberNumber })
            .ToListAsync();
        if (contributions.Count == 0)
            return (new List<EurPendingLine>(), new List<EurContributionProof>());

        List<Guid> ids = contributions.Select(x => x.Id).ToList();
        Dictionary<Guid, BookingNumberEntity> numbers = (await _db.BookingNumbers
                .Where(x => x.ContributionId != null && ids.Contains(x.ContributionId.Value))
                .ToListAsync())
            .ToDictionary(x => x.ContributionId!.Value);

        List<EurPendingLine> lines = contributions
            .GroupBy(x => x.PaymentDate.Date)
            .Select(g =>
            {
                List<BookingNumberEntity> groupNumbers = g
                    .Select(x => numbers.GetValueOrDefault(x.Id))
                    .OfType<BookingNumberEntity>()
                    .OrderBy(x => x.Sequence)
                    .ToList();
                string number = groupNumbers.Count == 0 ? string.Empty
                    : groupNumbers.Count == 1 ? groupNumbers[0].Number
                    : $"{groupNumbers[0].Number} bis {groupNumbers[^1].Number}";

                return new EurPendingLine(number, g.Key, 0, $"Beitragseinzug ({g.Count()} Beiträge)", EurLabels.Membership,
                    TaxArea.NON_PROFIT, true, EurLabels.Bank, g.Sum(x => x.Amount));
            })
            .ToList();

        List<EurContributionProof> proofs = contributions
            .Select(x => new EurContributionProof(numbers.GetValueOrDefault(x.Id)?.Number ?? string.Empty, x.PaymentDate.Date,
                x.DueDate.Date, x.MemberNumber, x.Amount))
            .OrderBy(x => x.PaymentDate)
            .ThenBy(x => x.Number, StringComparer.Ordinal)
            .ToList();

        return (lines, proofs);
    }

    private async Task<List<EurPendingLine>> LoadIncomeLinesAsync(int year)
    {
        var incomes = await _db.Incomes
            .Where(x => x.DeletedAt == null && x.Paid != null && x.PaymentDate != null && x.PaymentDate.Value.Year == year)
            .Select(x => new { x.Id, PaymentDate = x.PaymentDate!.Value, x.Payer, x.Description, x.Amount, x.CategoryId })
            .ToListAsync();
        if (incomes.Count == 0)
            return new List<EurPendingLine>();

        Dictionary<Guid, IncomeCategoryEntity> categories = (await _db.IncomeCategories.IgnoreQueryFilters().ToListAsync())
            .ToDictionary(x => x.Id);

        List<Guid> incomeIds = incomes.Select(x => x.Id).ToList();
        Dictionary<Guid, string> acknowledgements = (await _db.PaymentAcknowledgements
                .Where(x => x.IncomeId != null && incomeIds.Contains(x.IncomeId.Value))
                .ToListAsync())
            .GroupBy(x => x.IncomeId!.Value)
            .ToDictionary(g => g.Key, g => g.First().Number);
        Dictionary<Guid, string> bookingNumbers = await _bookingNumberService.GetIncomeNumbersAsync(incomeIds);

        return incomes.Select(income =>
        {
            categories.TryGetValue(income.CategoryId ?? Guid.Empty, out IncomeCategoryEntity? category);
            string text = string.IsNullOrWhiteSpace(income.Description) ? income.Payer : $"{income.Payer}: {income.Description}";
            if (acknowledgements.TryGetValue(income.Id, out string? number))
                text += $" (Quittung {number})";

            return new EurPendingLine(bookingNumbers.GetValueOrDefault(income.Id, string.Empty), income.PaymentDate.Date, 1, text,
                category?.Name ?? EurLabels.OtherIncome, category?.TaxArea ?? TaxArea.NON_PROFIT, true, null, income.Amount);
        }).ToList();
    }

    private async Task<List<EurPendingLine>> LoadReceiptLinesAsync(int year)
    {
        var receipts = await _db.Receipts
            .Where(x => x.DeletedAt == null && x.Paid != null && x.PaymentDate != null && x.PaymentDate.Value.Year == year)
            .Select(x => new { x.Id, PaymentDate = x.PaymentDate!.Value, x.Vendor, x.Description, x.Amount, x.CategoryId, x.PaymentMethod })
            .ToListAsync();
        if (receipts.Count == 0)
            return new List<EurPendingLine>();

        Dictionary<Guid, ReceiptCategoryEntity> categories = (await _db.ReceiptCategories.IgnoreQueryFilters().ToListAsync())
            .ToDictionary(x => x.Id);
        Dictionary<Guid, string> bookingNumbers = await _bookingNumberService.GetReceiptNumbersAsync(receipts.Select(x => x.Id).ToList());

        return receipts.Select(receipt =>
        {
            categories.TryGetValue(receipt.CategoryId ?? Guid.Empty, out ReceiptCategoryEntity? category);
            string text = string.Join(": ", new[] { receipt.Vendor, receipt.Description }.Where(x => !string.IsNullOrWhiteSpace(x)));
            string? account = receipt.PaymentMethod switch
            {
                null => null,
                ReceiptPaymentMethod.CASH => EurLabels.Cash,
                _ => EurLabels.Bank
            };

            return new EurPendingLine(bookingNumbers.GetValueOrDefault(receipt.Id, string.Empty), receipt.PaymentDate.Date, 2,
                text.Length == 0 ? "Beleg" : text, category?.Name ?? EurLabels.OtherExpense,
                category?.TaxArea ?? TaxArea.NON_PROFIT, false, account, receipt.Amount);
        }).ToList();
    }

    private async Task<string?> FindTreasurerAsync()
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();
        List<MemberEntity> candidates = await _db.Members
            .Where(x => x.DeletedAt == null && x.TaskWithinTheClub == TaskWithinTheClub.CHIEF_FINANCE_OFFICER)
            .ToListAsync();

        MemberEntity? treasurer = candidates
            .Where(x => x.ExitDate == null || x.ExitDate > now)
            .OrderBy(x => x.LastName, StringComparer.CurrentCulture)
            .ThenBy(x => x.FirstName, StringComparer.CurrentCulture)
            .FirstOrDefault();

        return treasurer == null ? null : $"{treasurer.FirstName} {treasurer.LastName}";
    }
}
