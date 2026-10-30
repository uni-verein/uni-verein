using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniVerein.Api.ApiResults.Finance;
using UniVerein.Api.Models.Enums;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using UniVerein.IntegrationTests.Infrastructure;
using Shouldly;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class FinanceControllerTests : IntegrationTestBase
{
    private readonly AppDbContext _db;
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public FinanceControllerTests(UniVereinWebApplicationFactory factory)
        : base(factory)
    {
        _db = GetService<AppDbContext>();
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _db.Receipts.RemoveRange(_db.Receipts.IgnoreQueryFilters());
        _db.ReceiptCategories.RemoveRange(_db.ReceiptCategories.IgnoreQueryFilters());
        _db.Incomes.RemoveRange(_db.Incomes.IgnoreQueryFilters());
        _db.IncomeCategories.RemoveRange(_db.IncomeCategories.IgnoreQueryFilters());
        _db.Contributions.RemoveRange(_db.Contributions.IgnoreQueryFilters());
        _db.Members.RemoveRange(_db.Members.IgnoreQueryFilters());
        await _db.ForceSaveChangesAsync();
    }

    // ---------------------------------------------------------------
    // GET /finance/analytics
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetAnalyticsAsync_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/finance/analytics");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAnalyticsAsync_Forbidden_ForUser()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.USER);

        // Act
        HttpResponseMessage response = await client.GetAsync("/finance/analytics");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task GetAnalyticsAsync_PrivilegedRoles_ReturnEmptyResultWithoutData(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);

        // Act
        FinanceAnalyticsResult result = await GetAnalyticsAsync(client);

        // Assert
        result.Rows.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAnalyticsAsync_Expenses_CountOnlyPaidReceiptsPerMonthAndCategory()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        ReceiptCategoryEntity eat = await CreateReceiptCategoryAsync("Eat");
        ReceiptCategoryEntity travel = await CreateReceiptCategoryAsync("Travel expenses");
        await CreateReceiptAsync(30m, Utc(2026, 4, 10), eat, paid: true);
        await CreateReceiptAsync(20m, Utc(2026, 4, 12), eat, paid: true);
        await CreateReceiptAsync(5m, Utc(2026, 4, 20), travel);

        // Act
        FinanceAnalyticsResult result = await GetAnalyticsAsync(client);

        // Assert
        FinanceAnalyticsRowResult row = result.Rows.ShouldHaveSingleItem();
        row.Kind.ShouldBe(FinanceEntryKind.EXPENSE);
        row.Year.ShouldBe(2026);
        row.Month.ShouldBe(4);
        row.CategoryId.ShouldBe(eat.Id);
        row.CategoryName.ShouldBe("Eat");
        row.Amount.ShouldBe(50m);
    }

    [Fact]
    public async Task GetAnalyticsAsync_AllKinds_UsePaymentDateInsteadOfDocumentDate()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        IncomeCategoryEntity donations = await CreateIncomeCategoryAsync("Spenden");
        ReceiptCategoryEntity eat = await CreateReceiptCategoryAsync("Eat");
        await CreateContributionAsync(12m, Utc(2025, 12, 31), paid: true, paymentDate: Utc(2026, 1, 5));
        await CreateIncomeAsync(donations, 50m, Utc(2025, 12, 20), paid: true, paymentDate: Utc(2026, 1, 3));
        await CreateReceiptAsync(30m, Utc(2025, 12, 28), eat, paid: true, paymentDate: Utc(2026, 1, 9));

        // Act
        FinanceAnalyticsResult result = await GetAnalyticsAsync(client);

        // Assert
        result.Rows.Count.ShouldBe(3);
        result.Rows.ShouldAllBe(r => r.Year == 2026 && r.Month == 1);
    }

    [Fact]
    public async Task GetAnalyticsAsync_Expenses_ExcludeDeletedReceipts()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        ReceiptCategoryEntity eat = await CreateReceiptCategoryAsync("Eat");
        await CreateReceiptAsync(10m, Utc(2026, 4, 10), eat, paid: true);
        await CreateReceiptAsync(99m, Utc(2026, 4, 10), eat, paid: true, deleted: true);

        // Act
        FinanceAnalyticsResult result = await GetAnalyticsAsync(client);

        // Assert
        result.Rows.ShouldHaveSingleItem().Amount.ShouldBe(10m);
    }

    [Fact]
    public async Task GetAnalyticsAsync_Expenses_WithoutCategoryHaveNoCategoryId()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        await CreateReceiptAsync(10m, Utc(2026, 4, 10), null, paid: true);

        // Act
        FinanceAnalyticsResult result = await GetAnalyticsAsync(client);

        // Assert
        FinanceAnalyticsRowResult row = result.Rows.ShouldHaveSingleItem();
        row.CategoryId.ShouldBeNull();
        row.CategoryName.ShouldBeNull();
    }

    [Fact]
    public async Task GetAnalyticsAsync_Membership_CountsOnlyPaidContributionsByPaymentDate()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.FINANCIAL_MANAGER);
        await CreateContributionAsync(12m, Utc(2026, 1, 31), paid: true);
        await CreateContributionAsync(8m, Utc(2026, 1, 15), paid: true);
        await CreateContributionAsync(40m, Utc(2026, 2, 1), paid: false);
        await CreateContributionAsync(99m, Utc(2026, 1, 15), paid: true, deleted: true);

        // Act
        FinanceAnalyticsResult result = await GetAnalyticsAsync(client);

        // Assert
        FinanceAnalyticsRowResult row = result.Rows.ShouldHaveSingleItem();
        row.Kind.ShouldBe(FinanceEntryKind.MEMBERSHIP);
        row.Year.ShouldBe(2026);
        row.Month.ShouldBe(1);
        row.CategoryId.ShouldBeNull();
        row.Amount.ShouldBe(20m);
    }

    [Fact]
    public async Task GetAnalyticsAsync_Income_CountsOnlyPaidIncomesByPaymentDate()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        IncomeCategoryEntity donations = await CreateIncomeCategoryAsync("Spenden");
        await CreateIncomeAsync(donations, 50m, Utc(2026, 6, 1), paid: true);
        await CreateIncomeAsync(donations, 25m, Utc(2026, 6, 30), paid: true);
        await CreateIncomeAsync(donations, 70m, Utc(2026, 7, 1), paid: false);
        await CreateIncomeAsync(donations, 99m, Utc(2026, 6, 1), paid: true, deleted: true);

        // Act
        FinanceAnalyticsResult result = await GetAnalyticsAsync(client);

        // Assert
        FinanceAnalyticsRowResult row = result.Rows.ShouldHaveSingleItem();
        row.Kind.ShouldBe(FinanceEntryKind.INCOME);
        row.Month.ShouldBe(6);
        row.CategoryId.ShouldBe(donations.Id);
        row.CategoryName.ShouldBe("Spenden");
        row.Amount.ShouldBe(75m);
    }

    [Fact]
    public async Task GetAnalyticsAsync_ReturnsAllYearsOrderedChronologically()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        IncomeCategoryEntity donations = await CreateIncomeCategoryAsync("Spenden");
        ReceiptCategoryEntity eat = await CreateReceiptCategoryAsync("Eat");
        await CreateReceiptAsync(15m, Utc(2026, 1, 1), eat, paid: true);
        await CreateIncomeAsync(donations, 40m, Utc(2025, 12, 31), paid: true);
        await CreateReceiptAsync(25m, Utc(2025, 1, 1), eat, paid: true);

        // Act
        FinanceAnalyticsResult result = await GetAnalyticsAsync(client);

        // Assert
        result.Rows.Select(r => (r.Year, r.Month, r.Kind)).ShouldBe(new[]
        {
            (2025, 1, FinanceEntryKind.EXPENSE),
            (2025, 12, FinanceEntryKind.INCOME),
            (2026, 1, FinanceEntryKind.EXPENSE)
        });
    }

    private async Task<FinanceAnalyticsResult> GetAnalyticsAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.GetAsync("/finance/analytics");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        FinanceAnalyticsResult? result = await response.Content.ReadFromJsonAsync<FinanceAnalyticsResult>(_jsonSerializerOptions);
        result.ShouldNotBeNull();
        return result;
    }

    private static DateTime Utc(int year, int month, int day) => new(year, month, day, 0, 0, 0, DateTimeKind.Utc);

    private async Task<ReceiptCategoryEntity> CreateReceiptCategoryAsync(string name)
    {
        ReceiptCategoryEntity category = new() { Id = Guid.NewGuid(), Name = name };
        await _db.ReceiptCategories.AddAsync(category);
        await _db.SaveChangesAsync();
        return category;
    }

    private async Task<IncomeCategoryEntity> CreateIncomeCategoryAsync(string name)
    {
        IncomeCategoryEntity category = new() { Id = Guid.NewGuid(), Name = name };
        await _db.IncomeCategories.AddAsync(category);
        await _db.SaveChangesAsync();
        return category;
    }

    private async Task CreateReceiptAsync(decimal amount, DateTime date, ReceiptCategoryEntity? category,
        bool paid = false, bool deleted = false, DateTime? paymentDate = null)
    {
        ReceiptEntity receipt = new()
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Amount = amount,
            ReceiptDate = date,
            CategoryId = category?.Id,
            Paid = paid ? DateTimeOffset.UtcNow : null,
            PaymentDate = paid ? paymentDate ?? date : null,
            DeletedAt = deleted ? DateTimeOffset.UtcNow : null
        };
        await _db.Receipts.AddAsync(receipt);
        await _db.SaveChangesAsync();
    }

    private async Task CreateIncomeAsync(IncomeCategoryEntity category, decimal amount, DateTime date,
        bool paid, bool deleted = false, DateTime? paymentDate = null)
    {
        IncomeEntity income = new()
        {
            Id = Guid.NewGuid(),
            CategoryId = category.Id,
            Payer = "Spender",
            Amount = amount,
            IncomeDate = date,
            Paid = paid ? DateTimeOffset.UtcNow : null,
            PaymentDate = paid ? paymentDate ?? date : null,
            DeletedAt = deleted ? DateTimeOffset.UtcNow : null
        };
        await _db.Incomes.AddAsync(income);
        await _db.SaveChangesAsync();
    }

    private async Task CreateContributionAsync(decimal amount, DateTime dueDate, bool paid, bool deleted = false, DateTime? paymentDate = null)
    {
        Guid memberId = Guid.NewGuid();
        MemberEntity member = new()
        {
            MandateId = memberId.ToString(),
            Id = memberId,
            FirstName = "Max",
            LastName = "Mustermann",
            BirthdayEncrypted = string.Empty,
            City = "Musterstadt",
            PostalCode = "12345",
            CountryCode = "DE",
            StreetEncrypted = string.Empty,
            MembershipStatus = MembershipStatus.MEMBER
        };
        ContributionEntity contribution = new()
        {
            Id = Guid.NewGuid(),
            MemberId = member.Id,
            MemberEntity = member,
            Amount = amount,
            DueDate = dueDate,
            Paid = paid ? DateTimeOffset.UtcNow : null,
            PaymentDate = paid ? paymentDate ?? dueDate : null,
            DeletedAt = deleted ? DateTimeOffset.UtcNow : null
        };
        await _db.Members.AddAsync(member);
        await _db.Contributions.AddAsync(contribution);
        await _db.SaveChangesAsync();
    }
}
