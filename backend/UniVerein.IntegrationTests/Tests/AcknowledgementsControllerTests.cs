using System.Net;
using System.Net.Http.Json;
using System.Text;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.Models.Enums;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using UniVerein.IntegrationTests.Infrastructure;
using Shouldly;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class AcknowledgementsControllerTests : IntegrationTestBase
{
    private readonly AppDbContext _db;

    public AcknowledgementsControllerTests(UniVereinWebApplicationFactory factory)
        : base(factory)
    {
        _db = GetService<AppDbContext>();
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _db.AuditLogs.RemoveRange(_db.AuditLogs.IgnoreQueryFilters());
        _db.PaymentAcknowledgements.RemoveRange(_db.PaymentAcknowledgements.IgnoreQueryFilters());
        _db.Incomes.RemoveRange(_db.Incomes.IgnoreQueryFilters());
        _db.IncomeCategories.RemoveRange(_db.IncomeCategories.IgnoreQueryFilters());
        _db.CreditorConfigs.RemoveRange(_db.CreditorConfigs.IgnoreQueryFilters());
        _db.Contributions.RemoveRange(_db.Contributions.IgnoreQueryFilters());
        _db.Members.RemoveRange(_db.Members.IgnoreQueryFilters());
        await _db.ForceSaveChangesAsync();
    }

    [Fact]
    public async Task IssueAsync_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/acknowledgements", Request(IncomeSource.MANUAL, Guid.NewGuid()));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task IssueAsync_Forbidden_ForUser()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.USER);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/acknowledgements", Request(IncomeSource.MANUAL, Guid.NewGuid()));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(IncomeSource.MANUAL)]
    [InlineData(IncomeSource.CONTRIBUTION)]
    public async Task IssueAsync_UnknownId_ReturnsNotFound(IncomeSource source)
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/acknowledgements", Request(source, Guid.NewGuid()));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(IncomeSource.MANUAL)]
    [InlineData(IncomeSource.CONTRIBUTION)]
    public async Task IssueAsync_UnpaidEntry_ReturnsBadRequest(IncomeSource source)
    {
        // Arrange
        Guid id = source == IncomeSource.MANUAL ? (await CreateIncomeAsync(paid: false)).Id : (await CreateContributionAsync(paid: false)).Id;
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/acknowledgements", Request(source, id));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await _db.PaymentAcknowledgements.CountAsync()).ShouldBe(0);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task IssueAsync_PaidIncome_ReturnsPdfAndStoresSnapshot(UserRole role)
    {
        // Arrange
        IncomeEntity income = await CreateIncomeAsync(paid: true);
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/acknowledgements", Request(IncomeSource.MANUAL, income.Id));
        byte[] pdf = await response.Content.ReadAsByteArrayAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        Encoding.ASCII.GetString(pdf, 0, 4).ShouldBe("%PDF");
        PaymentAcknowledgementEntity stored = await _db.PaymentAcknowledgements.SingleAsync();
        stored.Number.ShouldBe($"{DateTimeOffset.UtcNow.Year}-0001");
        stored.IncomeId.ShouldBe(income.Id);
        stored.PayerName.ShouldBe("Familie Krause");
        stored.Amount.ShouldBe(250);
        stored.Purpose.ShouldBe("Spenden: Spende fuer die Jugendarbeit");
        stored.IssuerName.ShouldBe("Verein");
    }

    [Fact]
    public async Task IssueAsync_PaidContribution_UsesMemberAndCreditorData()
    {
        // Arrange
        await _db.CreditorConfigs.AddAsync(new CreditorConfigEntity
        {
            Id = Guid.NewGuid(),
            Name = "Testverein e.V.",
            Iban_Encrypted = "x",
            Bic_Encrypted = "x",
            CreditorId = "DE98ZZZ09999999999",
            StreetNameAndNumber = "Vereinsweg 1",
            PostCode = "24103",
            CityName = "Kiel",
            CountryCode = "DE"
        });
        await _db.SaveChangesAsync();
        ContributionEntity contribution = await CreateContributionAsync(paid: true);
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/acknowledgements", Request(IncomeSource.CONTRIBUTION, contribution.Id));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        PaymentAcknowledgementEntity stored = await _db.PaymentAcknowledgements.SingleAsync();
        stored.ContributionId.ShouldBe(contribution.Id);
        stored.PayerName.ShouldBe("Hannah Berger");
        stored.PayerAddress.ShouldBe("12345 Musterstadt");
        stored.Purpose.ShouldBe("Mitgliedsbeitrag, fällig am 01.06.2026");
        stored.IssuerName.ShouldBe("Testverein e.V.");
        stored.IssuerAddress.ShouldBe("Vereinsweg 1, 24103 Kiel");
    }

    [Fact]
    public async Task IssueAsync_SecondCall_ReturnsSameAcknowledgement()
    {
        // Arrange
        IncomeEntity income = await CreateIncomeAsync(paid: true);
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        await client.PostAsJsonAsync("/acknowledgements", Request(IncomeSource.MANUAL, income.Id));
        HttpResponseMessage second = await client.PostAsJsonAsync("/acknowledgements", Request(IncomeSource.MANUAL, income.Id));

        // Assert
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _db.PaymentAcknowledgements.CountAsync()).ShouldBe(1);
        (await _db.AuditLogs.CountAsync(x => x.Entity == nameof(PaymentAcknowledgementEntity))).ShouldBe(1);
    }

    [Fact]
    public async Task IssueAsync_NumbersAreConsecutivePerYear()
    {
        // Arrange
        IncomeEntity first = await CreateIncomeAsync(paid: true);
        IncomeEntity second = await CreateIncomeAsync(paid: true);
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        await client.PostAsJsonAsync("/acknowledgements", Request(IncomeSource.MANUAL, first.Id));
        await client.PostAsJsonAsync("/acknowledgements", Request(IncomeSource.MANUAL, second.Id));

        // Assert
        List<int> sequences = await _db.PaymentAcknowledgements.OrderBy(x => x.Sequence).Select(x => x.Sequence).ToListAsync();
        sequences.ShouldBe(new List<int> { 1, 2 });
    }

    [Fact]
    public async Task IssueAsync_SnapshotSurvivesLaterChangesOfTheContribution()
    {
        // Arrange
        ContributionEntity contribution = await CreateContributionAsync(paid: true);
        HttpClient client = CreateClient(UserRole.ADMIN);
        await client.PostAsJsonAsync("/acknowledgements", Request(IncomeSource.CONTRIBUTION, contribution.Id));
        ContributionEntity tracked = await _db.Contributions.SingleAsync();
        tracked.Amount = 999;
        await _db.SaveChangesAsync();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/acknowledgements", Request(IncomeSource.CONTRIBUTION, contribution.Id));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _db.PaymentAcknowledgements.AsNoTracking().SingleAsync()).Amount.ShouldBe(12);
    }

    [Fact]
    public async Task ContributionMarkAsUnpaid_WhenAcknowledgementIssued_ReturnsConflict()
    {
        // Arrange
        ContributionEntity contribution = await CreateContributionAsync(paid: true);
        HttpClient client = CreateClient(UserRole.ADMIN);
        await client.PostAsJsonAsync("/acknowledgements", Request(IncomeSource.CONTRIBUTION, contribution.Id));

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync($"/contributions/{contribution.Id}", new MarkPaidRequest { Paid = false });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await _db.Contributions.AsNoTracking().SingleAsync()).Paid.ShouldNotBeNull();
    }

    [Fact]
    public async Task ContributionMarkAsPaid_WhenAcknowledgementIssued_ReturnsConflictAndKeepsPaymentDate()
    {
        // Arrange
        ContributionEntity contribution = await CreateContributionAsync(paid: true);
        HttpClient client = CreateClient(UserRole.ADMIN);
        await client.PostAsJsonAsync("/acknowledgements", Request(IncomeSource.CONTRIBUTION, contribution.Id));

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync($"/contributions/{contribution.Id}",
            new MarkPaidRequest { Paid = true, PaymentDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc) });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await _db.Contributions.AsNoTracking().SingleAsync()).PaymentDate.ShouldBe(contribution.PaymentDate);
    }

    [Fact]
    public async Task IncomeMarkAsPaid_WhenAcknowledgementIssued_ReturnsConflictAndKeepsPaymentDate()
    {
        // Arrange
        IncomeEntity income = await CreateIncomeAsync(paid: true);
        HttpClient client = CreateClient(UserRole.ADMIN);
        await client.PostAsJsonAsync("/acknowledgements", Request(IncomeSource.MANUAL, income.Id));

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync($"/income/{income.Id}",
            new MarkPaidRequest { Paid = true, PaymentDate = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc) });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await _db.Incomes.AsNoTracking().SingleAsync()).PaymentDate.ShouldBe(income.PaymentDate);
    }

    [Fact]
    public async Task IssueAsync_UsesThePaymentDateAsPaidAt()
    {
        // Arrange
        IncomeEntity income = await CreateIncomeAsync(paid: true);
        income.PaymentDate = new DateTime(2025, 12, 28, 0, 0, 0, DateTimeKind.Utc);
        await _db.SaveChangesAsync();
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/acknowledgements", Request(IncomeSource.MANUAL, income.Id));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        PaymentAcknowledgementEntity stored = await _db.PaymentAcknowledgements.AsNoTracking().SingleAsync();
        stored.PaidAt.UtcDateTime.ShouldBe(new DateTime(2025, 12, 28, 0, 0, 0, DateTimeKind.Utc));
    }

    private static AcknowledgementRequest Request(IncomeSource source, Guid id) => new() { Source = source, Id = id };

    private async Task<IncomeEntity> CreateIncomeAsync(bool paid)
    {
        IncomeCategoryEntity category = await _db.IncomeCategories.FirstOrDefaultAsync(x => x.Name == "Spenden")
                                        ?? new IncomeCategoryEntity { Id = Guid.NewGuid(), Name = "Spenden" };
        if (_db.Entry(category).State == EntityState.Detached)
            await _db.IncomeCategories.AddAsync(category);

        IncomeEntity income = new()
        {
            Id = Guid.NewGuid(),
            CategoryId = category.Id,
            Payer = "Familie Krause",
            Description = "Spende fuer die Jugendarbeit",
            Amount = 250,
            IncomeDate = new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc),
            Paid = paid ? DateTimeOffset.UtcNow : null,
            PaymentDate = paid ? new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc) : null
        };
        await _db.Incomes.AddAsync(income);
        await _db.SaveChangesAsync();
        return income;
    }

    private async Task<ContributionEntity> CreateContributionAsync(bool paid)
    {
        Guid memberId = Guid.NewGuid();
        MemberEntity member = new()
        {
            MandateId = memberId.ToString(),
            Id = memberId,
            FirstName = "Hannah",
            LastName = "Berger",
            BirthdayEncrypted = string.Empty,
            City = "Musterstadt",
            PostalCode = "12345",
            CountryCode = "DE",
            StreetEncrypted = string.Empty
        };
        ContributionEntity contribution = new()
        {
            Id = Guid.NewGuid(),
            MemberId = member.Id,
            MemberEntity = member,
            Amount = 12,
            DueDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            Paid = paid ? DateTimeOffset.UtcNow : null,
            PaymentDate = paid ? new DateTime(2026, 6, 3, 0, 0, 0, DateTimeKind.Utc) : null
        };
        await _db.Members.AddAsync(member);
        await _db.Contributions.AddAsync(contribution);
        await _db.SaveChangesAsync();
        return contribution;
    }
}
