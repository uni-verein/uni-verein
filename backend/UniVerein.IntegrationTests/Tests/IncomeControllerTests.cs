using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults.Income;
using UniVerein.Api.Models.Enums;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using UniVerein.IntegrationTests.Infrastructure;
using Shouldly;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class IncomeControllerTests : IntegrationTestBase
{
    private readonly AppDbContext _db;
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public IncomeControllerTests(UniVereinWebApplicationFactory factory)
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
        _db.Contributions.RemoveRange(_db.Contributions.IgnoreQueryFilters());
        _db.Members.RemoveRange(_db.Members.IgnoreQueryFilters());
        await _db.ForceSaveChangesAsync();
    }

    // ---------------------------------------------------------------
    // GET /income
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetAsync_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/income");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    [InlineData(UserRole.ADMIN)]
    public async Task GetAsync_AnyAuthenticatedRole_ReturnsRowsFromBothSources(UserRole role)
    {
        // Arrange
        await CreateContributionAsync("Max", "Mustermann", MembershipStatus.MEMBER);
        await CreateIncomeAsync("Spendenbox");
        HttpClient client = CreateClient(role);

        // Act
        IncomeRowsResult result = await GetRowsAsync(client, "");

        // Assert
        result.Total.ShouldBe(2);
        result.Items.Select(x => x.Source).ShouldBe(new[] { IncomeSource.CONTRIBUTION, IncomeSource.MANUAL }, ignoreOrder: true);
    }

    [Fact]
    public async Task GetAsync_InvalidPaging_ReturnsBadRequest()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.USER);

        // Act
        HttpResponseMessage response = await client.GetAsync("/income?limit=0");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetAsync_ByKind_ReturnsMatchingRows()
    {
        // Arrange
        IncomeCategoryEntity category = await CreateCategoryAsync("Sponsoring");
        await CreateContributionAsync("Max", "Mitglied", MembershipStatus.MEMBER);
        await CreateContributionAsync("Fritz", "Foerder", MembershipStatus.SUPPORTING_MEMBER);
        await CreateIncomeAsync("Baeckerei", category);
        HttpClient client = CreateClient(UserRole.USER);

        // Act
        IncomeRowsResult all = await GetRowsAsync(client, "");
        IncomeRowsResult membership = await GetRowsAsync(client, "?kind=MEMBERSHIP");
        IncomeRowsResult categoryRows = await GetRowsAsync(client, "?kind=CATEGORY");
        IncomeRowsResult byCategoryId = await GetRowsAsync(client, $"?categoryId={category.Id}");

        // Assert
        all.Total.ShouldBe(3);
        membership.Items.Select(x => x.Payer).ShouldBe(new[] { "Max Mitglied", "Fritz Foerder" }, ignoreOrder: true);
        membership.Items.ShouldAllBe(x => x.Kind == IncomeKind.MEMBERSHIP && x.Source == IncomeSource.CONTRIBUTION);
        categoryRows.Items.Select(x => x.Payer).ShouldBe(new[] { "Baeckerei" });
        categoryRows.Items.Single().CategoryName.ShouldBe("Sponsoring");
        byCategoryId.Items.Select(x => x.Payer).ShouldBe(new[] { "Baeckerei" });
    }

    [Theory]
    [InlineData(MembershipStatus.MEMBER, "Max Mitglied")]
    [InlineData(MembershipStatus.SUPPORTING_MEMBER, "Fritz Foerder")]
    public async Task GetAsync_ByMembershipStatus_OnlyReturnsContributionsOfThatStatus(MembershipStatus status, string expected)
    {
        // Arrange
        await CreateContributionAsync("Max", "Mitglied", MembershipStatus.MEMBER);
        await CreateContributionAsync("Fritz", "Foerder", MembershipStatus.SUPPORTING_MEMBER);
        await CreateIncomeAsync("Spendenbox");
        HttpClient client = CreateClient(UserRole.USER);

        // Act
        IncomeRowsResult result = await GetRowsAsync(client, $"?kind=MEMBERSHIP&membershipStatus={status}");

        // Assert
        result.Items.Select(x => x.Payer).ShouldBe(new[] { expected });
        result.Items.Single().MembershipStatus.ShouldBe(status);
    }

    [Fact]
    public async Task GetAsync_ByMemberNumber_OnlyReturnsContributionsOfThatMember()
    {
        // Arrange
        await CreateContributionAsync("Max", "Mitglied", MembershipStatus.MEMBER, memberNumber: 7);
        await CreateContributionAsync("Fritz", "Foerder", MembershipStatus.SUPPORTING_MEMBER, memberNumber: 8);
        await CreateIncomeAsync("Spendenbox");
        HttpClient client = CreateClient(UserRole.USER);

        // Act
        IncomeRowsResult result = await GetRowsAsync(client, "?kind=MEMBERSHIP&memberNumber=7");
        IncomeRowsResult withoutKind = await GetRowsAsync(client, "?memberNumber=7");

        // Assert
        result.Items.Select(x => x.Payer).ShouldBe(new[] { "Max Mitglied" });
        result.Items.Single().MemberNumber.ShouldBe(7);
        withoutKind.Items.Select(x => x.Payer).ShouldBe(new[] { "Max Mitglied" });
    }

    [Fact]
    public async Task GetAsync_ByCategoryId_OnlyReturnsThatCategory()
    {
        // Arrange
        IncomeCategoryEntity sponsoring = await CreateCategoryAsync("Sponsoring");
        IncomeCategoryEntity events = await CreateCategoryAsync("Veranstaltungen");
        await CreateIncomeAsync("Baeckerei", sponsoring);
        await CreateIncomeAsync("Sommerfest", events);
        HttpClient client = CreateClient(UserRole.USER);

        // Act
        IncomeRowsResult result = await GetRowsAsync(client, $"?categoryId={events.Id}");

        // Assert
        result.Items.Select(x => x.Payer).ShouldBe(new[] { "Sommerfest" });
    }

    [Theory]
    [InlineData(true, 2)]
    [InlineData(false, 1)]
    public async Task GetAsync_ByUnpaid_ReturnsMatchingRows(bool unpaid, int expected)
    {
        // Arrange
        await CreateContributionAsync("Open", "One", MembershipStatus.MEMBER);
        await CreateIncomeAsync("Open income");
        await CreateIncomeAsync("Paid income", paid: true);
        HttpClient client = CreateClient(UserRole.USER);

        // Act
        IncomeRowsResult result = await GetRowsAsync(client, $"?unpaid={unpaid.ToString().ToLowerInvariant()}");

        // Assert
        result.Total.ShouldBe(expected);
        result.Items.ShouldAllBe(x => x.Paid == !unpaid);
    }

    [Fact]
    public async Task GetAsync_ByDateRange_IncludesBoundariesOfBothSources()
    {
        // Arrange
        await CreateContributionAsync("Before", "Range", MembershipStatus.MEMBER, dueDate: Utc(2026, 2, 28));
        await CreateContributionAsync("Start", "Range", MembershipStatus.MEMBER, dueDate: Utc(2026, 3, 1));
        await CreateIncomeAsync("Middle", incomeDate: Utc(2026, 3, 15));
        await CreateIncomeAsync("End", incomeDate: Utc(2026, 3, 31));
        await CreateIncomeAsync("After", incomeDate: Utc(2026, 4, 1));
        HttpClient client = CreateClient(UserRole.USER);

        // Act
        IncomeRowsResult result = await GetRowsAsync(client, "?dateFrom=2026-03-01&dateTo=2026-03-31");

        // Assert
        result.Total.ShouldBe(3);
        result.Items.Select(x => x.Payer).ShouldBe(new[] { "End", "Middle", "Start Range" });
    }

    [Fact]
    public async Task GetAsync_OrdersOpenFirstThenNewestDate()
    {
        // Arrange
        await CreateIncomeAsync("Paid new", paid: true, incomeDate: Utc(2026, 5, 1));
        await CreateIncomeAsync("Open old", incomeDate: Utc(2026, 1, 1));
        await CreateContributionAsync("Open", "New", MembershipStatus.MEMBER, dueDate: Utc(2026, 4, 1));
        HttpClient client = CreateClient(UserRole.USER);

        // Act
        IncomeRowsResult result = await GetRowsAsync(client, "");

        // Assert
        result.Items.Select(x => x.Payer).ShouldBe(new[] { "Open New", "Open old", "Paid new" });
    }

    [Fact]
    public async Task GetAsync_PagesAcrossBothSources()
    {
        // Arrange
        for (int i = 1; i <= 3; i++)
        {
            await CreateContributionAsync("Member", i.ToString(), MembershipStatus.MEMBER, dueDate: Utc(2026, 1, i));
            await CreateIncomeAsync($"Income {i}", incomeDate: Utc(2026, 2, i));
        }

        HttpClient client = CreateClient(UserRole.USER);

        // Act
        IncomeRowsResult first = await GetRowsAsync(client, "?limit=4&offset=0");
        IncomeRowsResult second = await GetRowsAsync(client, "?limit=4&offset=4");

        // Assert
        first.Total.ShouldBe(6);
        second.Total.ShouldBe(6);
        first.Items.Count.ShouldBe(4);
        second.Items.Count.ShouldBe(2);
        first.Items.Concat(second.Items).Select(x => x.Id).Distinct().Count().ShouldBe(6);
    }

    [Fact]
    public async Task GetAsync_ByName_FindsContributionsAndIncomes()
    {
        // Arrange
        await CreateContributionAsync("Hannah", "Berger", MembershipStatus.MEMBER);
        await CreateContributionAsync("Jonas", "Weber", MembershipStatus.MEMBER);
        await CreateIncomeAsync("Familie Berger");
        await CreateIncomeAsync("Baeckerei", description: "Sponsoring Trikots");
        HttpClient client = CreateClient(UserRole.USER);

        // Act
        IncomeRowsResult byPayer = await GetRowsAsync(client, "?name=Berger");
        IncomeRowsResult byDescription = await GetRowsAsync(client, "?name=Trikots");

        // Assert
        byPayer.Items.Select(x => x.Payer).ShouldBe(new[] { "Hannah Berger", "Familie Berger" }, ignoreOrder: true);
        byDescription.Items.Select(x => x.Payer).ShouldBe(new[] { "Baeckerei" });
    }

    [Fact]
    public async Task GetAsync_DeletedFlag_OnlyHonouredForAdmin()
    {
        // Arrange
        await CreateIncomeAsync("Active");
        await CreateIncomeAsync("Removed", deleted: true);
        await CreateContributionAsync("Removed", "Member", MembershipStatus.MEMBER, deleted: true);

        // Act
        IncomeRowsResult asUser = await GetRowsAsync(CreateClient(UserRole.USER), "?deleted=true");
        IncomeRowsResult asAdmin = await GetRowsAsync(CreateClient(UserRole.ADMIN), "?deleted=true");

        // Assert
        asUser.Items.Select(x => x.Payer).ShouldBe(new[] { "Active" });
        asAdmin.Items.Select(x => x.Payer).ShouldBe(new[] { "Removed", "Removed Member" }, ignoreOrder: true);
        asAdmin.Items.ShouldAllBe(x => x.DeletedAt != null);
    }

    [Fact]
    public async Task GetAsync_KeepsOldPaidContributions()
    {
        // Arrange
        await CreateContributionAsync("Old", "Payer", MembershipStatus.MEMBER, paid: true, dueDate: Utc(2024, 1, 1));
        HttpClient client = CreateClient(UserRole.USER);

        // Act
        IncomeRowsResult result = await GetRowsAsync(client, "");

        // Assert
        result.Items.Select(x => x.Payer).ShouldBe(new[] { "Old Payer" });
    }

    // ---------------------------------------------------------------
    // GET /income/years
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetYearsAsync_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/income/years");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetYearsAsync_WithoutData_ReturnsOnlyCurrentYear()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.USER);

        // Act
        IncomeYearsResult result = await GetYearsAsync(client);

        // Assert
        result.Years.ShouldBe(new List<int> { DateTime.UtcNow.Year });
    }

    [Fact]
    public async Task GetYearsAsync_ReturnsAllYearsFromOldestToNewestDataNewestFirst()
    {
        // Arrange
        int currentYear = DateTime.UtcNow.Year;
        await CreateContributionAsync("Old", "Payer", MembershipStatus.MEMBER, dueDate: Utc(currentYear - 3, 5, 1));
        await CreateIncomeAsync("Future", incomeDate: Utc(currentYear + 1, 1, 15));
        HttpClient client = CreateClient(UserRole.USER);

        // Act
        IncomeYearsResult result = await GetYearsAsync(client);

        // Assert
        result.Years.ShouldBe(new List<int> { currentYear + 1, currentYear, currentYear - 1, currentYear - 2, currentYear - 3 });
    }

    // ---------------------------------------------------------------
    // GET /income/summary
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetSummaryAsync_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/income/summary");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetSummaryAsync_CountsOpenAlwaysAndPaidWithinRange()
    {
        // Arrange
        IncomeCategoryEntity category = await CreateCategoryAsync("Sponsoring");
        await CreateContributionAsync("Open", "Old", MembershipStatus.MEMBER, dueDate: Utc(2025, 1, 1), amount: 10);
        await CreateIncomeAsync("Open income", incomeDate: Utc(2025, 6, 1), amount: 5);
        await CreateContributionAsync("Paid", "Member", MembershipStatus.MEMBER, paid: true, dueDate: Utc(2026, 1, 1), amount: 12);
        await CreateContributionAsync("Paid", "Supporter", MembershipStatus.SUPPORTING_MEMBER, paid: true, dueDate: Utc(2026, 1, 1), amount: 20);
        await CreateIncomeAsync("Spendenbox", paid: true, incomeDate: Utc(2026, 2, 1), amount: 30);
        await CreateIncomeAsync("Baeckerei", category, paid: true, incomeDate: Utc(2026, 3, 1), amount: 100);
        await CreateIncomeAsync("Last year", paid: true, incomeDate: Utc(2025, 12, 31), amount: 999);
        HttpClient client = CreateClient(UserRole.USER);

        // Act
        HttpResponseMessage response = await client.GetAsync("/income/summary?dateFrom=2026-01-01&dateTo=2026-12-31");
        IncomeSummaryResult? result = await response.Content.ReadFromJsonAsync<IncomeSummaryResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.OpenAmount.ShouldBe(15);
        result.OpenCount.ShouldBe(2);
        result.PaidAmount.ShouldBe(162);
        result.PaidCount.ShouldBe(4);
        result.Breakdown.Single(x => x.Kind == IncomeKind.MEMBERSHIP).Amount.ShouldBe(32);
        result.Breakdown.Where(x => x.Kind == IncomeKind.CATEGORY).Select(x => x.CategoryName)
            .ShouldBe(new[] { "Spenden", "Sponsoring" });
        IncomeBreakdownResult categoryBreakdown = result.Breakdown.Single(x => x.CategoryId == category.Id);
        categoryBreakdown.Amount.ShouldBe(100);
    }

    [Fact]
    public async Task GetSummaryAsync_CountsPaidByPaymentDateAndKeepsUncategorizedIncomes()
    {
        // Arrange
        await CreateContributionAsync("Late", "Payer", MembershipStatus.MEMBER, paid: true, dueDate: Utc(2025, 12, 1), amount: 12,
            paymentDate: Utc(2026, 1, 4));
        await CreateContributionAsync("Early", "Payer", MembershipStatus.MEMBER, paid: true, dueDate: Utc(2026, 1, 1), amount: 20,
            paymentDate: Utc(2025, 12, 30));
        await CreateIncomeAsync("Late donation", paid: true, incomeDate: Utc(2025, 12, 15), amount: 30, paymentDate: Utc(2026, 1, 2));
        await CreateIncomeAsync("Orphan", paid: true, incomeDate: Utc(2026, 2, 1), amount: 7, withoutCategory: true);
        HttpClient client = CreateClient(UserRole.USER);

        // Act
        HttpResponseMessage response = await client.GetAsync("/income/summary?dateFrom=2026-01-01&dateTo=2026-12-31");
        IncomeSummaryResult? result = await response.Content.ReadFromJsonAsync<IncomeSummaryResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.PaidAmount.ShouldBe(49);
        result.PaidCount.ShouldBe(3);
        result.Breakdown.Single(x => x.Kind == IncomeKind.MEMBERSHIP).Amount.ShouldBe(12);
        result.Breakdown.Single(x => x.Kind == IncomeKind.CATEGORY && x.CategoryId == null).Amount.ShouldBe(7);
    }

    // ---------------------------------------------------------------
    // POST /income
    // ---------------------------------------------------------------

    [Fact]
    public async Task CreateAsync_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/income", CreateRequest(Guid.NewGuid()));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateAsync_Forbidden_ForUser()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.USER);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/income", CreateRequest(Guid.NewGuid()));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task CreateAsync_Success_Paid(UserRole role)
    {
        // Arrange
        IncomeCategoryEntity category = await CreateCategoryAsync("Spenden");
        HttpClient client = CreateClient(role);
        IncomeRequest request = CreateRequest(category.Id);
        request.Paid = true;

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/income", request);
        IncomeRowResult? result = await response.Content.ReadFromJsonAsync<IncomeRowResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        result.ShouldNotBeNull();
        result.Source.ShouldBe(IncomeSource.MANUAL);
        result.Kind.ShouldBe(IncomeKind.CATEGORY);
        result.CategoryName.ShouldBe("Spenden");
        result.Paid.ShouldBeTrue();
        result.Payer.ShouldBe("Familie Krause");
        IncomeEntity stored = await _db.Incomes.SingleAsync();
        stored.Amount.ShouldBe(250);
        stored.Paid.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateAsync_Success_Open()
    {
        // Arrange
        IncomeCategoryEntity category = await CreateCategoryAsync("Sponsoring");
        HttpClient client = CreateClient(UserRole.FINANCIAL_MANAGER);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/income", CreateRequest(category.Id));
        IncomeRowResult? result = await response.Content.ReadFromJsonAsync<IncomeRowResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        result.ShouldNotBeNull();
        result.CategoryName.ShouldBe("Sponsoring");
        result.Paid.ShouldBeFalse();
    }

    [Fact]
    public async Task CreateAsync_WritesAuditLog()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        IncomeCategoryEntity category = await CreateCategoryAsync("Spenden");
        HttpResponseMessage response = await client.PostAsJsonAsync("/income", CreateRequest(category.Id));
        IncomeRowResult? result = await response.Content.ReadFromJsonAsync<IncomeRowResult>(_jsonSerializerOptions);

        // Assert
        AuditLogEntity? auditLog = await _db.AuditLogs.FirstOrDefaultAsync(l =>
            l.Entity == nameof(IncomeEntity) && l.Data.Contains(result!.Id.ToString()));
        auditLog.ShouldNotBeNull();
        auditLog.Action.ShouldBe(nameof(AuditLogActions.CREATE));
    }

    [Theory]
    [InlineData(0, "Payer")]
    [InlineData(-5, "Payer")]
    [InlineData(10, "   ")]
    public async Task CreateAsync_InvalidRequest_ReturnsBadRequest(decimal amount, string payer)
    {
        // Arrange
        IncomeCategoryEntity category = await CreateCategoryAsync("Spenden");
        HttpClient client = CreateClient(UserRole.ADMIN);
        IncomeRequest request = CreateRequest(category.Id);
        request.Amount = amount;
        request.Payer = payer;

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/income", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await _db.Incomes.CountAsync()).ShouldBe(0);
    }

    [Theory]
    [InlineData("1000000.01")]
    [InlineData("79228162514264337593543950335")]
    public async Task CreateAsync_AmountAboveLimit_ReturnsBadRequest(string amount)
    {
        // Arrange
        IncomeCategoryEntity category = await CreateCategoryAsync("Spenden");
        HttpClient client = CreateClient(UserRole.ADMIN);
        IncomeRequest request = CreateRequest(category.Id);
        request.Amount = decimal.Parse(amount, CultureInfo.InvariantCulture);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/income", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await _db.Incomes.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task CreateAsync_AmountAtLimit_IsAccepted()
    {
        // Arrange
        IncomeCategoryEntity category = await CreateCategoryAsync("Spenden");
        HttpClient client = CreateClient(UserRole.ADMIN);
        IncomeRequest request = CreateRequest(category.Id);
        request.Amount = 1_000_000m;

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/income", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateAsync_UnknownCategory_ReturnsBadRequest()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response =
            await client.PostAsJsonAsync("/income", CreateRequest(Guid.NewGuid()));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ---------------------------------------------------------------
    // PUT /income/{id}
    // ---------------------------------------------------------------

    [Fact]
    public async Task UpdateAsync_Forbidden_ForUser()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.USER);

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync($"/income/{Guid.NewGuid()}", UpdateRequest(Guid.NewGuid()));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateAsync_NotFound()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync($"/income/{Guid.NewGuid()}", UpdateRequest(Guid.NewGuid()));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateAsync_Success_ChangesFieldsButNotPaidState()
    {
        // Arrange
        IncomeCategoryEntity category = await CreateCategoryAsync("Sponsoring");
        IncomeEntity income = await CreateIncomeAsync("Old payer", paid: true);
        HttpClient client = CreateClient(UserRole.FINANCIAL_MANAGER);
        IncomeUpdateRequest request = UpdateRequest(category.Id);

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync($"/income/{income.Id}", request);
        IncomeRowResult? result = await response.Content.ReadFromJsonAsync<IncomeRowResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.Kind.ShouldBe(IncomeKind.CATEGORY);
        result.Payer.ShouldBe("New payer");
        result.Paid.ShouldBeTrue();
        IncomeEntity stored = await _db.Incomes.AsNoTracking().SingleAsync();
        stored.CategoryId.ShouldBe(category.Id);
        stored.Amount.ShouldBe(75);
    }

    [Fact]
    public async Task UpdateAsync_InvalidAmount_ReturnsBadRequest()
    {
        // Arrange
        IncomeEntity income = await CreateIncomeAsync("Payer");
        HttpClient client = CreateClient(UserRole.ADMIN);
        IncomeUpdateRequest request = UpdateRequest(income.CategoryId!.Value);
        request.Amount = 0;

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync($"/income/{income.Id}", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UpdateAsync_AmountAboveLimit_ReturnsBadRequest()
    {
        // Arrange
        IncomeEntity income = await CreateIncomeAsync("Payer");
        HttpClient client = CreateClient(UserRole.ADMIN);
        IncomeUpdateRequest request = UpdateRequest(income.CategoryId!.Value);
        request.Amount = 1_000_000.01m;

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync($"/income/{income.Id}", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    // ---------------------------------------------------------------
    // POST /income/{id}
    // ---------------------------------------------------------------

    [Fact]
    public async Task MarkAsPaidAsync_Forbidden_ForUser()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.USER);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync($"/income/{Guid.NewGuid()}", new MarkPaidRequest { Paid = true });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task MarkAsPaidAsync_NotFound()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync($"/income/{Guid.NewGuid()}", new MarkPaidRequest { Paid = true });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MarkAsPaidAsync_TogglesPaidState_AndWritesAuditLog(bool paid)
    {
        // Arrange
        IncomeEntity income = await CreateIncomeAsync("Payer", paid: !paid);
        HttpClient client = CreateClient(UserRole.FINANCIAL_MANAGER);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync($"/income/{income.Id}", new MarkPaidRequest { Paid = paid });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        IncomeEntity stored = await _db.Incomes.AsNoTracking().SingleAsync();
        (stored.Paid != null).ShouldBe(paid);
        AuditLogEntity? auditLog = await _db.AuditLogs.FirstOrDefaultAsync(l =>
            l.Entity == nameof(IncomeEntity) && l.Data.Contains(income.Id.ToString()));
        auditLog.ShouldNotBeNull();
        auditLog.Action.ShouldBe(nameof(AuditLogActions.UPDATE));
    }

    [Fact]
    public async Task MarkAsPaidAsync_Reopen_WhenAcknowledgementIssued_ReturnsConflict()
    {
        // Arrange
        IncomeEntity income = await CreateIncomeAsync("Payer", paid: true);
        await _db.PaymentAcknowledgements.AddAsync(new PaymentAcknowledgementEntity
        {
            Id = Guid.NewGuid(),
            Year = 2026,
            Sequence = 1,
            IncomeId = income.Id,
            PayerName = "Payer",
            Amount = 50,
            Purpose = "Spenden",
            PaidAt = DateTimeOffset.UtcNow,
            IssuerName = "Verein"
        });
        await _db.SaveChangesAsync();
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync($"/income/{income.Id}", new MarkPaidRequest { Paid = false });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await _db.Incomes.AsNoTracking().SingleAsync()).Paid.ShouldNotBeNull();
    }

    [Fact]
    public async Task UpdateAsync_WhenAcknowledgementIssued_ReturnsConflict()
    {
        // Arrange
        IncomeEntity income = await CreateIncomeAsync("Payer", paid: true);
        await _db.PaymentAcknowledgements.AddAsync(new PaymentAcknowledgementEntity
        {
            Id = Guid.NewGuid(),
            Year = 2026,
            Sequence = 1,
            IncomeId = income.Id,
            PayerName = "Payer",
            Amount = 50,
            Purpose = "Spenden",
            PaidAt = DateTimeOffset.UtcNow,
            IssuerName = "Verein"
        });
        await _db.SaveChangesAsync();
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync($"/income/{income.Id}", UpdateRequest(income.CategoryId!.Value));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetAsync_ShowsAcknowledgementNumber()
    {
        // Arrange
        IncomeEntity income = await CreateIncomeAsync("Payer", paid: true);
        await _db.PaymentAcknowledgements.AddAsync(new PaymentAcknowledgementEntity
        {
            Id = Guid.NewGuid(),
            Year = 2026,
            Sequence = 7,
            IncomeId = income.Id,
            PayerName = "Payer",
            Amount = 50,
            Purpose = "Spenden",
            PaidAt = DateTimeOffset.UtcNow,
            IssuerName = "Verein"
        });
        await _db.SaveChangesAsync();
        await CreateIncomeAsync("Other", paid: true);
        HttpClient client = CreateClient(UserRole.USER);

        // Act
        IncomeRowsResult result = await GetRowsAsync(client, "");

        // Assert
        result.Items.Single(x => x.Payer == "Payer").AcknowledgementNumber.ShouldBe("2026-0007");
        result.Items.Single(x => x.Payer == "Other").AcknowledgementNumber.ShouldBeNull();
    }

    // ---------------------------------------------------------------
    // DELETE /income/{id} and POST /income/{id}/restore
    // ---------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_Forbidden_ForUser()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.USER);

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/income/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteAsync_NotFound()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/income/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteAsync_OpenIncome_SoftDeletes()
    {
        // Arrange
        IncomeEntity income = await CreateIncomeAsync("Payer");
        HttpClient client = CreateClient(UserRole.FINANCIAL_MANAGER);

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/income/{income.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        IncomeEntity stored = await _db.Incomes.AsNoTracking().SingleAsync();
        stored.DeletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task DeleteAsync_PaidIncome_ReturnsBadRequest()
    {
        // Arrange
        IncomeEntity income = await CreateIncomeAsync("Payer", paid: true);
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/income/{income.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await _db.Incomes.AsNoTracking().SingleAsync()).DeletedAt.ShouldBeNull();
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task RestoreAsync_Forbidden_WhenNotAdmin(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response = await client.PostAsync($"/income/{Guid.NewGuid()}/restore", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RestoreAsync_Admin_RestoresDeletedIncome()
    {
        // Arrange
        IncomeEntity income = await CreateIncomeAsync("Payer", deleted: true);
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.PostAsync($"/income/{income.Id}/restore", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _db.Incomes.AsNoTracking().SingleAsync()).DeletedAt.ShouldBeNull();
    }

    [Fact]
    public async Task RestoreAsync_NotDeletedIncome_ReturnsNotFound()
    {
        // Arrange
        IncomeEntity income = await CreateIncomeAsync("Payer");
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.PostAsync($"/income/{income.Id}/restore", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------
    // Helper functions
    // ---------------------------------------------------------------

    private async Task<IncomeRowsResult> GetRowsAsync(HttpClient client, string queryString)
    {
        HttpResponseMessage response = await client.GetAsync($"/income{queryString}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        IncomeRowsResult? result = await response.Content.ReadFromJsonAsync<IncomeRowsResult>(_jsonSerializerOptions);
        result.ShouldNotBeNull();
        return result;
    }

    private async Task<IncomeYearsResult> GetYearsAsync(HttpClient client)
    {
        HttpResponseMessage response = await client.GetAsync("/income/years");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        IncomeYearsResult? result = await response.Content.ReadFromJsonAsync<IncomeYearsResult>(_jsonSerializerOptions);
        result.ShouldNotBeNull();
        return result;
    }

    private static DateTime Utc(int year, int month, int day) => new(year, month, day, 0, 0, 0, DateTimeKind.Utc);

    private static IncomeRequest CreateRequest(Guid categoryId)
    {
        return new IncomeRequest
        {
            CategoryId = categoryId,
            Payer = "Familie Krause",
            Description = "Spende fuer die Jugendarbeit",
            Amount = 250,
            IncomeDate = Utc(2026, 10, 5)
        };
    }

    private static IncomeUpdateRequest UpdateRequest(Guid categoryId)
    {
        return new IncomeUpdateRequest
        {
            CategoryId = categoryId,
            Payer = "New payer",
            Description = "Changed",
            Amount = 75,
            IncomeDate = Utc(2026, 11, 1)
        };
    }

    private async Task<IncomeCategoryEntity> CreateCategoryAsync(string name)
    {
        IncomeCategoryEntity category = new() { Id = Guid.NewGuid(), Name = name };
        await _db.IncomeCategories.AddAsync(category);
        await _db.SaveChangesAsync();
        return category;
    }

    private async Task<IncomeEntity> CreateIncomeAsync(string payer, IncomeCategoryEntity? category = null,
        bool paid = false, bool deleted = false, DateTime? incomeDate = null, decimal amount = 50, string? description = null,
        DateTime? paymentDate = null, bool withoutCategory = false)
    {
        category ??= await _db.IncomeCategories.FirstOrDefaultAsync(x => x.Name == "Spenden")
                     ?? await CreateCategoryAsync("Spenden");
        IncomeEntity income = new()
        {
            Id = Guid.NewGuid(),
            CategoryId = withoutCategory ? null : category.Id,
            Payer = payer,
            Description = description,
            Amount = amount,
            IncomeDate = incomeDate ?? Utc(2026, 6, 1),
            Paid = paid ? DateTimeOffset.UtcNow : null,
            PaymentDate = paid ? paymentDate ?? incomeDate ?? Utc(2026, 6, 1) : null,
            DeletedAt = deleted ? DateTimeOffset.UtcNow : null
        };
        await _db.Incomes.AddAsync(income);
        await _db.SaveChangesAsync();
        return income;
    }

    private async Task<ContributionEntity> CreateContributionAsync(string firstName, string lastName,
        MembershipStatus membershipStatus, bool paid = false, bool deleted = false, DateTime? dueDate = null,
        decimal amount = 12, int memberNumber = 0, DateTime? paymentDate = null)
    {
        Guid memberId = Guid.NewGuid();
        MemberEntity member = new()
        {
            MandateId = memberId.ToString(),
            Id = memberId,
            FirstName = firstName,
            LastName = lastName,
            BirthdayEncrypted = string.Empty,
            City = "Musterstadt",
            PostalCode = "12345",
            CountryCode = "DE",
            StreetEncrypted = string.Empty,
            MembershipStatus = membershipStatus,
            MemberNumber = memberNumber
        };
        ContributionEntity contribution = new()
        {
            Id = Guid.NewGuid(),
            MemberId = member.Id,
            MemberEntity = member,
            Amount = amount,
            DueDate = dueDate ?? Utc(2026, 6, 1),
            Paid = paid ? DateTimeOffset.UtcNow : null,
            PaymentDate = paid ? paymentDate ?? dueDate ?? Utc(2026, 6, 1) : null,
            DeletedAt = deleted ? DateTimeOffset.UtcNow : null
        };
        await _db.Members.AddAsync(member);
        await _db.Contributions.AddAsync(contribution);
        await _db.SaveChangesAsync();
        return contribution;
    }
}
