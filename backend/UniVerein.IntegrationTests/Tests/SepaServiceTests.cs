using UniVerein.Api.Data;
using UniVerein.Api.Data.Enums;
using UniVerein.Api.Services;
using UniVerein.Api.Services.Sepa;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using UniVerein.IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class SepaServiceTests : IntegrationTestBase
{
    private readonly AppDbContext _db;
    private readonly SepaService _sepaService;

    private const string ValidIban = "DE89370400440532013000";
    private const string ValidBic = "COBADEFFXXX";
    private const string DecryptedStreet = "Musterstraße 1";
    private static readonly Guid DefaultExportId = Guid.NewGuid();

    private static readonly CreditorConfig ValidCreditor = new()
    {
        Name = "Musterverein e.V.",
        Iban = ValidIban,
        Bic = ValidBic,
        CreditorId = "DE98ZZZ09999999999",
        StreetName = "Vereinsstraße 1",
        PostCode = "12345",
        TownName = "Musterstadt",
        Country = "DE"
    };

    public SepaServiceTests(UniVereinWebApplicationFactory factory) : base(factory)
    {
        _db = GetService<AppDbContext>();
        _sepaService = new SepaService(_db, GetService<CryptoService>());
    }

    public override async Task InitializeAsync()
    {
        await WithDbContext(async db =>
        {
            db.Members.RemoveRange(db.Members.AsQueryable());
            db.Contributions.RemoveRange(db.Contributions.AsQueryable());
            db.SepaExports.RemoveRange(db.SepaExports.AsQueryable());
            await db.ForceSaveChangesAsync();
        });
    }

    public override async Task DisposeAsync()
    {
        await WithDbContext(async db =>
        {
            db.Members.RemoveRange(db.Members.AsQueryable());
            db.Contributions.RemoveRange(db.Contributions.AsQueryable());
            db.SepaExports.RemoveRange(db.SepaExports.AsQueryable());
            await db.ForceSaveChangesAsync();
        });
    }

    [Fact]
    public async Task GenerateXml_WithValidData_ReturnsNonEmptyXml()
    {
        // Arrange
        MemberEntity member = await CreateMemberAsync();
        await CreateDueContributionAsync(member);

        // Act
        var (xml, amount, count) = await _sepaService.GenerateXml(ValidCreditor, DefaultExportId);

        // Assert
        xml.ShouldNotBeNullOrWhiteSpace();
        xml.ShouldContain("<Document");
        amount.ShouldBe(120m);
        count.ShouldBe(1);
    }

    [Fact]
    public async Task GenerateXml_SumsAmountsCorrectly_WhenMultipleContributions()
    {
        // Arrange
        MemberEntity member1 = await CreateMemberAsync(1, "MANDATE_001");
        MemberEntity member2 = await CreateMemberAsync(2, "MANDATE_002");
        await CreateDueContributionAsync(member1, DefaultExportId, 120m);
        await CreateDueContributionAsync(member2, DefaultExportId, 60m);

        // Act
        var (xml, amount, count) = await _sepaService.GenerateXml(ValidCreditor, DefaultExportId);

        // Assert
        amount.ShouldBe(180m);
        count.ShouldBe(2);
        xml.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GenerateXml_UsesRCURSequenceType()
    {
        // Arrange
        MemberEntity member = await CreateMemberAsync();
        await CreateDueContributionAsync(member);

        // Act
        (string xml, _, _) = await _sepaService.GenerateXml(ValidCreditor, DefaultExportId);

        // Assert
        xml.ShouldContain(SequenceType.RCUR.ToString());
    }

    [Fact]
    public async Task GenerateXml_ContainsCreditorName_InXml()
    {
        // Arrange
        MemberEntity member = await CreateMemberAsync();
        await CreateDueContributionAsync(member);

        // Act
        (string xml, _, _) = await _sepaService.GenerateXml(ValidCreditor, DefaultExportId);

        // Assert
        xml.ShouldContain(ValidCreditor.Name);
    }

    [Fact]
    public async Task GenerateXml_ContainsCreditorIban_InXml()
    {
        // Arrange
        MemberEntity member = await CreateMemberAsync();
        await CreateDueContributionAsync(member);

        // Act
        (string xml, _, _) = await _sepaService.GenerateXml(ValidCreditor, DefaultExportId);

        // Assert
        xml.ShouldContain(ValidIban);
    }

    [Fact]
    public async Task GenerateXml_MonthlyInterval_FormatsRemittanceWithYearMonth()
    {
        // Arrange
        MemberEntity member = await CreateMemberAsync(interval: Interval.MONTHLY);
        ContributionEntity contribution = await CreateDueContributionAsync(member);
        string expectedDatePart = contribution.DueDate.ToString("yyyy-MM");

        // Act
        (string xml, _, _) = await _sepaService.GenerateXml(ValidCreditor, DefaultExportId);

        // Assert
        xml.ShouldContain(expectedDatePart);
    }

    [Fact]
    public async Task GenerateXml_YearlyInterval_FormatsRemittanceWithYearOnly()
    {
        // Arrange
        MemberEntity member = await CreateMemberAsync();
        ContributionEntity contribution = await CreateDueContributionAsync(member, dueDate: DateTime.Today.AddDays(-1));
        string expectedYear = contribution.DueDate.ToString("yyyy");

        // Act
        (string xml, _, _) = await _sepaService.GenerateXml(ValidCreditor, DefaultExportId);

        // Assert
        xml.ShouldContain($"Membership fee {expectedYear}");
    }

    [Fact]
    public async Task GenerateXml_ExcludesPaidContributions()
    {
        // Arrange
        MemberEntity member = await CreateMemberAsync();
        await CreateDueContributionAsync(member, paid: DateTime.Today.AddDays(-1));

        // Act
        (string xml, _, _) = await _sepaService.GenerateXml(ValidCreditor, DefaultExportId);

        // Assert
        xml.ShouldBeEmpty();
    }

    [Fact]
    public async Task GenerateXml_ExcludesFutureContributions()
    {
        // Arrange
        MemberEntity member = await CreateMemberAsync();
        await CreateDueContributionAsync(member, dueDate: DateTime.Today.AddDays(30));

        // Act
        (string xml, _, _) = await _sepaService.GenerateXml(ValidCreditor, DefaultExportId);

        // Assert
        xml.ShouldBeEmpty();
    }

    [Fact]
    public async Task GenerateXml_ExcludesContributions_WithoutSepaConsent()
    {
        // Arrange
        MemberEntity member = CreateMember();
        member.SepaConsent = null;
        await _db.Members.AddAsync(member);
        await _db.SaveChangesAsync();
        await CreateDueContributionAsync(member);

        // Act
        (string xml, _, _) = await _sepaService.GenerateXml(ValidCreditor, DefaultExportId);

        // Assert
        xml.ShouldBeEmpty();
    }

    [Fact]
    public async Task GenerateXml_ExcludesContributions_WithoutIban()
    {
        // Arrange
        MemberEntity member = CreateMember();
        member.IBAN_Encrypted = null;
        await _db.Members.AddAsync(member);
        await _db.SaveChangesAsync();
        await CreateDueContributionAsync(member);

        // Act
        (string xml, _, _) = await _sepaService.GenerateXml(ValidCreditor, DefaultExportId);

        // Assert
        xml.ShouldBeEmpty();
    }

    [Fact]
    public async Task GenerateXml_OnlyProcessesContributions_ForGivenExportId()
    {
        // Arrange
        MemberEntity member1 = await CreateMemberAsync(1, "MANDATE_001");
        MemberEntity member2 = await CreateMemberAsync(2, "MANDATE_001");
        ContributionEntity contribution = await CreateDueContributionAsync(member1, Guid.NewGuid(), 8019m);
        await CreateDueContributionAsync(member2, Guid.NewGuid(), 50m);

        // Act
        var (_, amount, count) = await _sepaService.GenerateXml(ValidCreditor, contribution.ExportId);

        // Assert
        count.ShouldBe(1);
        amount.ShouldBe(contribution.Amount);
    }

    [Fact]
    public async Task GenerateXml_ThrowsInvalidOperationException_WhenNoContributionsFound()
    {
        // Act
        (string xml, _, _) = await _sepaService.GenerateXml(ValidCreditor, DefaultExportId);

        // Assert
        xml.ShouldBeEmpty();
    }

    [Fact]
    public async Task GenerateXml_ThrowsException_WhenCreditorConfigIsInvalid()
    {
        // Arrange
        CreditorConfig invalidCreditor = new()
        {
            Name = "",
            TownName = "",
            Country = "DE",
            Iban = ValidIban,
            Bic = ValidBic,
            CreditorId = "DE98ZZZ09999999999"
        };

        // Act & Assert
        await Should.ThrowAsync<Exception>(async () => await _sepaService.GenerateXml(invalidCreditor, DefaultExportId));
    }

    [Fact]
    public async Task GenerateXml_RemovesSpaces_FromDecryptedIban()
    {
        // Arrange
        string iban = "DE89 3704 0044 0532 0130 00";
        MemberEntity member = await CreateMemberAsync(iban: iban);
        await CreateDueContributionAsync(member);

        // Act
        (string xml, _, _) = await _sepaService.GenerateXml(ValidCreditor, DefaultExportId);

        // Assert
        xml.ShouldContain(iban.Replace(" ", ""));
        xml.ShouldNotContain(iban);
    }

    [Fact]
    public async Task GenerateXml_InstructionId_IsNotLongerThan35Characters()
    {
        // Arrange
        MemberEntity member = await CreateMemberAsync(memberNumber: 1234567890, mandateId: "MANDATE_001");
        await CreateDueContributionAsync(member);

        // Act
        (string xml, _, _) = await _sepaService.GenerateXml(ValidCreditor, DefaultExportId);

        // Assert
        List<string> instrIds = ExtractTagValues(xml, "InstrId").ToList();
        foreach (var id in instrIds)
            id.Length.ShouldBeLessThanOrEqualTo(35, $"InstrId '{id}' exceeds max length of 35");
    }

    [Fact]
    public async Task GenerateXml_MandateId_ReplacesUnderscoresWithHyphens()
    {
        // Arrange
        MemberEntity member = await CreateMemberAsync(mandateId: "MANDATE_001_ABC");
        await CreateDueContributionAsync(member);

        // Act
        (string xml, _, _) = await _sepaService.GenerateXml(ValidCreditor, DefaultExportId);

        // Assert
        xml.ShouldContain("MANDATE-001-ABC");
        xml.ShouldNotContain("MANDATE_001_ABC");
    }

    private async Task<MemberEntity> CreateMemberAsync(int? memberNumber = null, string? mandateId = null,
        DateTimeOffset? sepaConsent = null, string? iban = null, Interval? interval = null)
    {
        MemberEntity member = CreateMember(memberNumber, mandateId, sepaConsent, iban, interval);

        await _db.ContributionPlans.AddAsync(member.ContributionPlan!);
        await _db.Members.AddAsync(member);
        await _db.SaveChangesAsync();

        return member;
    }

    private MemberEntity CreateMember(int? memberNumber = null, string? mandateId = null,
        DateTimeOffset? sepaConsent = null, string? iban = null, Interval? interval = null)
    {
        CryptoService cryptoService = GetService<CryptoService>();
        ContributionPlanEntity contributionPlan = new()
        {
            Id = Guid.NewGuid(),
            Interval = interval ?? Interval.YEARLY
        };

        MemberEntity member = new()
        {
            Id = Guid.NewGuid(),
            MemberNumber = memberNumber ?? 1,
            FirstName = "Max",
            LastName = "Mustermann",
            BirthdayEncrypted = string.Empty,
            IBAN_Encrypted = cryptoService.Encrypt(iban ?? ValidIban),
            Bic_Encrypted = cryptoService.Encrypt(ValidBic),
            StreetEncrypted = cryptoService.Encrypt(DecryptedStreet),
            PostalCode = "12345",
            City = "Musterstadt",
            CountryCode = "DE",
            MandateId = mandateId ?? "MANDATE_001",
            SepaConsent = sepaConsent ?? DateTimeOffset.UtcNow.AddYears(-1),
            ContributionPlan = contributionPlan,
            ContributionPlanId = contributionPlan.Id
        };

        return member;
    }

    private async Task<ContributionEntity> CreateDueContributionAsync(MemberEntity member, Guid? exportId = null,
        decimal? amount = null, DateTime? dueDate = null, DateTimeOffset? paid = null)
    {
        ContributionEntity contribution = new()
        {
            Id = Guid.NewGuid(),
            ExportId = exportId ?? DefaultExportId,
            Amount = amount ?? 120m,
            DueDate = dueDate ?? DateTime.Today.AddDays(-1),
            Paid = paid,
            MemberEntity = member
        };

        await _db.Contributions.AddAsync(contribution);
        await _db.SaveChangesAsync();

        return contribution;
    }

    private static IEnumerable<string> ExtractTagValues(string xml, string tagName)
    {
        List<string> results = new();
        string open = $"<{tagName}>";
        string close = $"</{tagName}>";
        int idx = 0;

        while ((idx = xml.IndexOf(open, idx, StringComparison.Ordinal)) >= 0)
        {
            int start = idx + open.Length;
            int end = xml.IndexOf(close, start, StringComparison.Ordinal);
            if (end < 0) break;
            results.Add(xml[start..end]);
            idx = end + close.Length;
        }

        return results;
    }
}