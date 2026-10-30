using System.Net;
using ClosedXML.Excel;
using UniVerein.Api.Services;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using UniVerein.IntegrationTests.Infrastructure;
using Shouldly;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class EurExportTests : IntegrationTestBase
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly AppDbContext _db;
    private readonly BookingNumberService _bookingNumbers;

    public EurExportTests(UniVereinWebApplicationFactory factory)
        : base(factory)
    {
        _db = GetService<AppDbContext>();
        _bookingNumbers = GetService<BookingNumberService>();
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _db.BookingNumbers.RemoveRange(_db.BookingNumbers.IgnoreQueryFilters());
        _db.Receipts.RemoveRange(_db.Receipts.IgnoreQueryFilters());
        _db.ReceiptCategories.RemoveRange(_db.ReceiptCategories.IgnoreQueryFilters());
        _db.Incomes.RemoveRange(_db.Incomes.IgnoreQueryFilters());
        _db.IncomeCategories.RemoveRange(_db.IncomeCategories.IgnoreQueryFilters());
        _db.Contributions.RemoveRange(_db.Contributions.IgnoreQueryFilters());
        _db.Members.RemoveRange(_db.Members.IgnoreQueryFilters());
        _db.CreditorConfigs.RemoveRange(_db.CreditorConfigs.IgnoreQueryFilters());
        await _db.ForceSaveChangesAsync();
    }

    // ---------------------------------------------------------------
    // GET /finance/eur
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetEurAsync_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/finance/eur?year=2026");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetEurAsync_Forbidden_ForUser()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.USER);

        // Act
        HttpResponseMessage response = await client.GetAsync("/finance/eur?year=2026");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(2019)]
    [InlineData(1999)]
    [InlineData(0)]
    [InlineData(3000)]
    public async Task GetEurAsync_YearOutOfRange_ReturnsBadRequest(int year)
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.GetAsync($"/finance/eur?year={year}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task GetEurAsync_PrivilegedRoles_ReturnWorkbookWithFourSheets(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response = await client.GetAsync("/finance/eur?year=2026");
        using XLWorkbook workbook = await ReadWorkbookAsync(response);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe(XlsxContentType);
        response.Content.Headers.ContentDisposition!.FileName.ShouldBe("EUER_2026.xlsx");
        workbook.Worksheets.Select(x => x.Name).ShouldBe(new[] { "EÜR", "Stammdaten", "Journal", "Beitragsnachweis", "Hinweise" });
    }

    [Fact]
    public async Task GetEurAsync_WithoutData_ReturnsEmptyJournalAndZeroTotals()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        using XLWorkbook workbook = await ReadWorkbookAsync(await client.GetAsync("/finance/eur?year=2026"));

        // Assert
        workbook.Worksheet("Journal").Cell("A5").IsEmpty().ShouldBeTrue();
        workbook.Worksheet("EÜR").Cell("F11").GetDouble().ShouldBe(0);
        workbook.Worksheet("EÜR").Cell("H11").GetDouble().ShouldBe(0);
    }

    [Fact]
    public async Task GetEurAsync_BooksOnlyPaidEntriesOfTheRequestedYear()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        IncomeCategoryEntity donations = await CreateIncomeCategoryAsync("Spenden", TaxArea.NON_PROFIT);
        ReceiptCategoryEntity material = await CreateReceiptCategoryAsync("Material", TaxArea.NON_PROFIT);
        await CreateIncomeAsync(donations, "Bäckerei", 500m, Utc(2026, 2, 1), paid: true);
        await CreateIncomeAsync(donations, "Offen", 70m, Utc(2026, 2, 2), paid: false);
        await CreateIncomeAsync(donations, "Altjahr", 40m, Utc(2025, 12, 31), paid: true);
        await CreateIncomeAsync(donations, "Gelöscht", 99m, Utc(2026, 2, 3), paid: true, deleted: true);
        await CreateReceiptAsync(100m, Utc(2026, 3, 1), material, paid: true, ReceiptPaymentMethod.BANK_TRANSFER);
        await CreateReceiptAsync(999m, Utc(2026, 3, 2), material, paid: false, ReceiptPaymentMethod.BANK_TRANSFER);
        await CreateContributionAsync(12m, Utc(2026, 1, 31), paid: true);
        await CreateContributionAsync(8m, Utc(2026, 1, 31), paid: true);
        await CreateContributionAsync(40m, Utc(2026, 2, 28), paid: false);

        // Act
        using XLWorkbook workbook = await ReadWorkbookAsync(await client.GetAsync("/finance/eur?year=2026"));

        // Assert
        IXLWorksheet journal = workbook.Worksheet("Journal");
        journal.Cell("A5").GetString().ShouldBe("E-2026-0003 bis E-2026-0004");
        journal.Cell("C5").GetString().ShouldBe("Beitragseinzug (2 Beiträge)");
        journal.Cell("D5").GetString().ShouldBe("Mitgliedsbeiträge");
        journal.Cell("H5").GetDouble().ShouldBe(20);
        journal.Cell("G5").GetString().ShouldBe("Bank");
        journal.Cell("C6").GetString().ShouldBe("Bäckerei");
        journal.Cell("A6").GetString().ShouldBe("E-2026-0001");
        journal.Cell("A7").GetString().ShouldBe("A-2026-0001");
        journal.Cell("B6").GetDateTime().ShouldBe(new DateTime(2026, 2, 1));
        journal.Cell("C7").GetString().ShouldBe("Beleg");
        journal.Cell("I7").GetDouble().ShouldBe(100);
        journal.Cell("A8").IsEmpty().ShouldBeTrue();
        journal.Cell("H3").GetDouble().ShouldBe(520);
        journal.Cell("I3").GetDouble().ShouldBe(100);
    }

    [Fact]
    public async Task GetEurAsync_UsesPaymentDateInsteadOfDocumentDate()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        ReceiptCategoryEntity material = await CreateReceiptCategoryAsync("Material", TaxArea.NON_PROFIT);
        await CreateReceiptAsync(80m, Utc(2025, 12, 28), material, paid: true, ReceiptPaymentMethod.BANK_TRANSFER, Utc(2026, 1, 5));

        // Act
        using XLWorkbook workbook2025 = await ReadWorkbookAsync(await client.GetAsync("/finance/eur?year=2025"));
        using XLWorkbook workbook2026 = await ReadWorkbookAsync(await client.GetAsync("/finance/eur?year=2026"));

        // Assert
        workbook2025.Worksheet("Journal").Cell("A5").IsEmpty().ShouldBeTrue();
        IXLWorksheet journal = workbook2026.Worksheet("Journal");
        journal.Cell("A5").GetString().ShouldBe("A-2026-0001");
        journal.Cell("B5").GetDateTime().ShouldBe(new DateTime(2026, 1, 5));
        journal.Cell("I5").GetDouble().ShouldBe(80);
    }

    [Fact]
    public async Task GetEurAsync_GroupsContributionsByPaymentDateAndListsThemInTheProofSheet()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        await CreateContributionAsync(12m, Utc(2026, 1, 31), paid: true, Utc(2026, 2, 2));
        await CreateContributionAsync(8m, Utc(2026, 2, 28), paid: true, Utc(2026, 2, 2));
        await CreateContributionAsync(5m, Utc(2026, 2, 28), paid: true, Utc(2026, 3, 1));

        // Act
        using XLWorkbook workbook = await ReadWorkbookAsync(await client.GetAsync("/finance/eur?year=2026"));

        // Assert
        IXLWorksheet journal = workbook.Worksheet("Journal");
        journal.Cell("A5").GetString().ShouldBe("E-2026-0001 bis E-2026-0002");
        journal.Cell("B5").GetDateTime().ShouldBe(new DateTime(2026, 2, 2));
        journal.Cell("C5").GetString().ShouldBe("Beitragseinzug (2 Beiträge)");
        journal.Cell("H5").GetDouble().ShouldBe(20);
        journal.Cell("A6").GetString().ShouldBe("E-2026-0003");

        IXLWorksheet proof = workbook.Worksheet("Beitragsnachweis");
        proof.Cell("A5").GetString().ShouldBe("E-2026-0001");
        proof.Cell("B5").GetDateTime().ShouldBe(new DateTime(2026, 2, 2));
        proof.Cell("C5").GetDateTime().ShouldBe(new DateTime(2026, 1, 31));
        proof.Cell("E5").GetDouble().ShouldBe(12);
        proof.Cell("A7").GetString().ShouldBe("E-2026-0003");
        proof.Cell("A8").IsEmpty().ShouldBeTrue();
    }

    [Fact]
    public async Task GetEurAsync_NumbersStayTheSameWhenAnOlderEntryIsAddedLater()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        IncomeCategoryEntity donations = await CreateIncomeCategoryAsync("Spenden", TaxArea.NON_PROFIT);
        await CreateIncomeAsync(donations, "Spät", 50m, Utc(2026, 6, 1), paid: true);
        using XLWorkbook before = await ReadWorkbookAsync(await client.GetAsync("/finance/eur?year=2026"));
        await CreateIncomeAsync(donations, "Früh", 30m, Utc(2026, 1, 1), paid: true);

        // Act
        using XLWorkbook after = await ReadWorkbookAsync(await client.GetAsync("/finance/eur?year=2026"));

        // Assert
        before.Worksheet("Journal").Cell("A5").GetString().ShouldBe("E-2026-0001");
        IXLWorksheet journal = after.Worksheet("Journal");
        journal.Cell("C5").GetString().ShouldBe("Früh");
        journal.Cell("A5").GetString().ShouldBe("E-2026-0002");
        journal.Cell("C6").GetString().ShouldBe("Spät");
        journal.Cell("A6").GetString().ShouldBe("E-2026-0001");
    }

    [Fact]
    public async Task GetEurAsync_SumsCategoriesPerTaxArea()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        IncomeCategoryEntity donations = await CreateIncomeCategoryAsync("Spenden", TaxArea.NON_PROFIT);
        IncomeCategoryEntity rent = await CreateIncomeCategoryAsync("Pacht", TaxArea.ASSET_MANAGEMENT);
        IncomeCategoryEntity kiosk = await CreateIncomeCategoryAsync("Kiosk", TaxArea.COMMERCIAL_OPERATION);
        await CreateIncomeCategoryAsync("Camps", TaxArea.PURPOSE_RELATED_OPERATION);
        ReceiptCategoryEntity tent = await CreateReceiptCategoryAsync("Zelt", TaxArea.COMMERCIAL_OPERATION);
        await CreateIncomeAsync(donations, "Spender", 500m, Utc(2026, 2, 1), paid: true);
        await CreateIncomeAsync(rent, "Pächter", 3000m, Utc(2026, 3, 1), paid: true);
        await CreateIncomeAsync(kiosk, "Fest", 1000m, Utc(2026, 5, 1), paid: true);
        await CreateIncomeAsync(kiosk, "Fest", 250m, Utc(2026, 5, 2), paid: true);
        await CreateReceiptAsync(200m, Utc(2026, 5, 3), tent, paid: true, ReceiptPaymentMethod.CASH);
        await CreateContributionAsync(20m, Utc(2026, 1, 31), paid: true);

        // Act
        using XLWorkbook workbook = await ReadWorkbookAsync(await client.GetAsync("/finance/eur?year=2026"));

        // Assert
        IXLWorksheet eur = workbook.Worksheet("EÜR");
        eur.Cell("E7").GetString().ShouldBe("1 Ideeller Bereich");
        eur.Cell("F7").GetDouble().ShouldBe(520);
        eur.Cell("E8").GetString().ShouldBe("2 Vermögensverwaltung");
        eur.Cell("F8").GetDouble().ShouldBe(3000);
        eur.Cell("F9").GetDouble().ShouldBe(0);
        eur.Cell("F10").GetDouble().ShouldBe(1250);
        eur.Cell("G10").GetDouble().ShouldBe(200);
        eur.Cell("H10").GetDouble().ShouldBe(1050);
        eur.Cell("F11").GetDouble().ShouldBe(4770);
        eur.Cell("G11").GetDouble().ShouldBe(200);
        eur.Cell("H11").GetDouble().ShouldBe(4570);

        IXLCell kioskCell = FindInColumn(eur, 1, "Kiosk");
        eur.Cell(kioskCell.Address.RowNumber, 2).GetDouble().ShouldBe(1250);
        eur.Cell(kioskCell.Address.RowNumber, 3).GetDouble().ShouldBe(1);
        FindInColumn(eur, 1, "Camps").Address.RowNumber.ShouldBeGreaterThan(0);
        IXLCell tentCell = FindInColumn(eur, 1, "Zelt");
        eur.Cell(tentCell.Address.RowNumber, 2).GetDouble().ShouldBe(200);

        IXLWorksheet journal = workbook.Worksheet("Journal");
        IXLCell tentLine = FindInColumn(journal, 4, "Zelt");
        journal.Cell(tentLine.Address.RowNumber, 7).GetString().ShouldBe("Kasse");
        journal.Cell(tentLine.Address.RowNumber, 5).GetString().ShouldBe("4 Wirtschaftlicher Geschäftsbetrieb");
        journal.Cell(tentLine.Address.RowNumber, 6).GetString().ShouldBe("Ausgabe");
    }

    [Fact]
    public async Task GetEurAsync_WgbIncomeAboveAllowance_FlagsTaxChecks()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        IncomeCategoryEntity kiosk = await CreateIncomeCategoryAsync("Kiosk", TaxArea.COMMERCIAL_OPERATION);
        await CreateIncomeAsync(kiosk, "Fest", 60000m, Utc(2026, 5, 1), paid: true);

        // Act
        using XLWorkbook workbook = await ReadWorkbookAsync(await client.GetAsync("/finance/eur?year=2026"));

        // Assert
        IXLWorksheet eur = workbook.Worksheet("EÜR");
        eur.Cell("F16").GetDouble().ShouldBe(60000);
        eur.Cell("G16").GetDouble().ShouldBe(50000);
        eur.Cell("H16").GetString().ShouldBe("✖ Freigrenze überschritten!");
        eur.Cell("H17").GetString().ShouldBe("✖ Abgrenzung Z/WGB nötig");
        eur.Cell("H18").GetString().ShouldBe("✔ Keine Pflicht");
    }

    [Fact]
    public async Task GetEurAsync_BeforeRaisedLimits_UsesTheOldLimitsAndNote()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        IncomeCategoryEntity kiosk = await CreateIncomeCategoryAsync("Kiosk", TaxArea.COMMERCIAL_OPERATION);
        await CreateIncomeAsync(kiosk, "Fest", 46000m, Utc(2025, 5, 1), paid: true);

        // Act
        using XLWorkbook workbook = await ReadWorkbookAsync(await client.GetAsync("/finance/eur?year=2025"));

        // Assert
        IXLWorksheet eur = workbook.Worksheet("EÜR");
        eur.Cell("F16").GetDouble().ShouldBe(46000);
        eur.Cell("G16").GetDouble().ShouldBe(45000);
        eur.Cell("H16").GetString().ShouldBe("✖ Freigrenze überschritten!");
        eur.Cell("G18").GetDouble().ShouldBe(45000);
        eur.Cell("E19").GetString().ShouldContain("2025");
    }

    [Fact]
    public async Task GetEurAsync_WgbIncomeAtAllowance_StaysFree()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        IncomeCategoryEntity kiosk = await CreateIncomeCategoryAsync("Kiosk", TaxArea.COMMERCIAL_OPERATION);
        await CreateIncomeAsync(kiosk, "Fest", 50000m, Utc(2026, 5, 1), paid: true);

        // Act
        using XLWorkbook workbook = await ReadWorkbookAsync(await client.GetAsync("/finance/eur?year=2026"));

        // Assert
        workbook.Worksheet("EÜR").Cell("H16").GetString().ShouldBe("✔ KSt/GewSt frei");
    }

    [Fact]
    public async Task GetEurAsync_UncategorizedEntries_LandInOtherCategoriesOfTheIdeellArea()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        _db.Incomes.Add(new IncomeEntity
        {
            Id = Guid.NewGuid(),
            Payer = "Anonym",
            Amount = 15m,
            IncomeDate = Utc(2026, 4, 1),
            Paid = DateTimeOffset.UtcNow,
            PaymentDate = Utc(2026, 4, 1)
        });
        await _db.SaveChangesAsync();
        await CreateReceiptAsync(25m, Utc(2026, 4, 2), null, paid: true, null);

        // Act
        using XLWorkbook workbook = await ReadWorkbookAsync(await client.GetAsync("/finance/eur?year=2026"));

        // Assert
        IXLWorksheet eur = workbook.Worksheet("EÜR");
        eur.Cell(FindInColumn(eur, 1, "Sonstige Einnahmen").Address.RowNumber, 2).GetDouble().ShouldBe(15);
        eur.Cell(FindInColumn(eur, 1, "Sonstige Ausgaben").Address.RowNumber, 2).GetDouble().ShouldBe(25);
        eur.Cell("H7").GetDouble().ShouldBe(-10);
    }

    [Fact]
    public async Task GetEurAsync_CashAndBankBalances_AreLeftEmptyForManualEntry()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        IncomeCategoryEntity donations = await CreateIncomeCategoryAsync("Spenden", TaxArea.NON_PROFIT);
        await CreateIncomeAsync(donations, "Spender", 500m, Utc(2026, 2, 1), paid: true);

        // Act
        using XLWorkbook workbook = await ReadWorkbookAsync(await client.GetAsync("/finance/eur?year=2026"));

        // Assert
        IXLWorksheet eur = workbook.Worksheet("EÜR");
        eur.Cell("E24").GetString().ShouldBe("Anfangsbestand 01.01.");
        eur.Cell("E27").GetString().ShouldBe("Endbestand 31.12.");
        foreach (string address in new[] { "F24", "G24", "H24", "F25", "G25", "H25", "F26", "G26", "H26", "F27", "G27", "H27" })
            eur.Cell(address).IsEmpty().ShouldBeTrue(address);

        IXLWorksheet masterData = workbook.Worksheet("Stammdaten");
        masterData.Cell("B10").IsEmpty().ShouldBeTrue();
        masterData.Cell("B11").IsEmpty().ShouldBeTrue();
    }

    [Fact]
    public async Task GetEurAsync_MasterData_ContainsClubTreasurerAndCategories()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        await CreateCreditorAsync("TSV Musterstadt e.V.", "00/000/00000");
        await CreateTreasurerAsync("Erika", "Kassenwart");
        await CreateIncomeCategoryAsync("Pacht", TaxArea.ASSET_MANAGEMENT);
        await CreateReceiptCategoryAsync("Material", TaxArea.NON_PROFIT);

        // Act
        using XLWorkbook workbook = await ReadWorkbookAsync(await client.GetAsync("/finance/eur?year=2026"));

        // Assert
        IXLWorksheet masterData = workbook.Worksheet("Stammdaten");
        masterData.Cell("B3").GetString().ShouldBe("TSV Musterstadt e.V.");
        masterData.Cell("B5").GetString().ShouldBe("00/000/00000");
        masterData.Cell("B6").GetString().ShouldBe("Erika Kassenwart");

        IXLCell pacht = FindInColumn(masterData, 1, "Pacht");
        masterData.Cell(pacht.Address.RowNumber, 2).GetString().ShouldBe("2 Vermögensverwaltung");
        masterData.Cell(pacht.Address.RowNumber, 3).GetString().ShouldBe("Einnahme");
        IXLCell material = FindInColumn(masterData, 1, "Material");
        masterData.Cell(material.Address.RowNumber, 2).GetString().ShouldBe("1 Ideeller Bereich");
        masterData.Cell(material.Address.RowNumber, 3).GetString().ShouldBe("Ausgabe");

        workbook.Worksheet("EÜR").Cell("A2").GetString().ShouldBe("TSV Musterstadt e.V.   ·   Steuernummer 00/000/00000");
    }

    [Fact]
    public async Task GetEurAsync_FormulaLikeText_IsNeutralised()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        IncomeCategoryEntity donations = await CreateIncomeCategoryAsync("Spenden", TaxArea.NON_PROFIT);
        await CreateIncomeAsync(donations, "=HYPERLINK(\"http://evil\")", 10m, Utc(2026, 2, 1), paid: true);

        // Act
        using XLWorkbook workbook = await ReadWorkbookAsync(await client.GetAsync("/finance/eur?year=2026"));

        // Assert
        IXLCell cell = workbook.Worksheet("Journal").Cell("C5");
        cell.HasFormula.ShouldBeFalse();
        cell.DataType.ShouldBe(XLDataType.Text);
        cell.Style.IncludeQuotePrefix.ShouldBeTrue();
        cell.GetString().ShouldBe("=HYPERLINK(\"http://evil\")");
    }

    [Fact]
    public async Task GetEurAsync_ContainsNoFormulas()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        IncomeCategoryEntity donations = await CreateIncomeCategoryAsync("Spenden", TaxArea.NON_PROFIT);
        await CreateIncomeAsync(donations, "Spender", 500m, Utc(2026, 2, 1), paid: true);

        // Act
        using XLWorkbook workbook = await ReadWorkbookAsync(await client.GetAsync("/finance/eur?year=2026"));

        // Assert
        workbook.Worksheets
            .SelectMany(x => x.CellsUsed())
            .Count(x => x.HasFormula)
            .ShouldBe(0);
    }

    private static async Task<XLWorkbook> ReadWorkbookAsync(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return new XLWorkbook(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
    }

    private static IXLCell FindInColumn(IXLWorksheet worksheet, int column, string text)
    {
        IXLCell? cell = worksheet.Column(column).CellsUsed().FirstOrDefault(x => x.GetString() == text);
        cell.ShouldNotBeNull($"'{text}' not found in column {column} of {worksheet.Name}");
        return cell;
    }

    private static DateTime Utc(int year, int month, int day) => new(year, month, day, 0, 0, 0, DateTimeKind.Utc);

    private async Task<IncomeCategoryEntity> CreateIncomeCategoryAsync(string name, TaxArea area)
    {
        IncomeCategoryEntity category = new() { Id = Guid.NewGuid(), Name = name, TaxArea = area };
        await _db.IncomeCategories.AddAsync(category);
        await _db.SaveChangesAsync();
        return category;
    }

    private async Task<ReceiptCategoryEntity> CreateReceiptCategoryAsync(string name, TaxArea area)
    {
        ReceiptCategoryEntity category = new() { Id = Guid.NewGuid(), Name = name, TaxArea = area };
        await _db.ReceiptCategories.AddAsync(category);
        await _db.SaveChangesAsync();
        return category;
    }

    private async Task CreateIncomeAsync(IncomeCategoryEntity category, string payer, decimal amount, DateTime date,
        bool paid, bool deleted = false, DateTime? paymentDate = null)
    {
        await _db.Incomes.AddAsync(new IncomeEntity
        {
            Id = Guid.NewGuid(),
            CategoryId = category.Id,
            Payer = payer,
            Amount = amount,
            IncomeDate = date,
            Paid = paid ? DateTimeOffset.UtcNow : null,
            PaymentDate = paid ? paymentDate ?? date : null,
            DeletedAt = deleted ? DateTimeOffset.UtcNow : null
        });
        await _db.SaveChangesAsync();
        await _bookingNumbers.AssignMissingAsync();
    }

    private async Task CreateReceiptAsync(decimal amount, DateTime date, ReceiptCategoryEntity? category, bool paid,
        ReceiptPaymentMethod? paymentMethod, DateTime? paymentDate = null)
    {
        await _db.Receipts.AddAsync(new ReceiptEntity
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Amount = amount,
            ReceiptDate = date,
            CategoryId = category?.Id,
            PaymentMethod = paymentMethod,
            Paid = paid ? DateTimeOffset.UtcNow : null,
            PaymentDate = paid ? paymentDate ?? date : null
        });
        await _db.SaveChangesAsync();
        await _bookingNumbers.AssignMissingAsync();
    }

    private async Task CreateContributionAsync(decimal amount, DateTime dueDate, bool paid, DateTime? paymentDate = null)
    {
        MemberEntity member = NewMember("Max", "Mustermann");
        await _db.Members.AddAsync(member);
        await _db.Contributions.AddAsync(new ContributionEntity
        {
            Id = Guid.NewGuid(),
            MemberId = member.Id,
            MemberEntity = member,
            Amount = amount,
            DueDate = dueDate,
            Paid = paid ? DateTimeOffset.UtcNow : null,
            PaymentDate = paid ? paymentDate ?? dueDate : null
        });
        await _db.SaveChangesAsync();
        await _bookingNumbers.AssignMissingAsync();
    }

    private async Task CreateTreasurerAsync(string firstName, string lastName)
    {
        MemberEntity member = NewMember(firstName, lastName);
        member.TaskWithinTheClub = TaskWithinTheClub.CHIEF_FINANCE_OFFICER;
        await _db.Members.AddAsync(member);
        await _db.SaveChangesAsync();
    }

    private async Task CreateCreditorAsync(string name, string taxNumber)
    {
        await _db.CreditorConfigs.AddAsync(new CreditorConfigEntity
        {
            Id = Guid.NewGuid(),
            Name = name,
            Iban_Encrypted = string.Empty,
            Bic_Encrypted = string.Empty,
            CreditorId = "DE98ZZZ09999999999",
            CityName = "Musterstadt",
            CountryCode = "DE",
            TaxNumber = taxNumber
        });
        await _db.SaveChangesAsync();
    }

    private static MemberEntity NewMember(string firstName, string lastName)
    {
        Guid id = Guid.NewGuid();
        return new MemberEntity
        {
            Id = id,
            MandateId = id.ToString(),
            FirstName = firstName,
            LastName = lastName,
            BirthdayEncrypted = string.Empty,
            City = "Musterstadt",
            PostalCode = "12345",
            CountryCode = "DE",
            StreetEncrypted = string.Empty,
            MembershipStatus = MembershipStatus.MEMBER
        };
    }
}
