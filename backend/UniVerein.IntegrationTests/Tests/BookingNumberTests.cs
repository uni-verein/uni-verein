using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults.Income;
using UniVerein.Api.ApiResults.Receipt;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using UniVerein.IntegrationTests.Infrastructure;
using Shouldly;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class BookingNumberTests : IntegrationTestBase
{
    private readonly AppDbContext _db;
    private readonly JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public BookingNumberTests(UniVereinWebApplicationFactory factory)
        : base(factory)
    {
        _db = GetService<AppDbContext>();
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _db.BookingNumbers.RemoveRange(_db.BookingNumbers.IgnoreQueryFilters());
        _db.PaymentAcknowledgements.RemoveRange(_db.PaymentAcknowledgements.IgnoreQueryFilters());
        _db.Receipts.RemoveRange(_db.Receipts.IgnoreQueryFilters());
        _db.Incomes.RemoveRange(_db.Incomes.IgnoreQueryFilters());
        _db.IncomeCategories.RemoveRange(_db.IncomeCategories.IgnoreQueryFilters());
        _db.Contributions.RemoveRange(_db.Contributions.IgnoreQueryFilters());
        _db.Members.RemoveRange(_db.Members.IgnoreQueryFilters());
        await _db.ForceSaveChangesAsync();
    }

    [Fact]
    public async Task CreateIncomeAsync_Paid_AssignsConsecutiveNumbersPerYear()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.FINANCIAL_MANAGER);
        IncomeCategoryEntity category = await CreateCategoryAsync();

        // Act
        IncomeRowResult first = await CreateIncomeAsync(client, category, Utc(2026, 3, 1), paid: true);
        IncomeRowResult second = await CreateIncomeAsync(client, category, Utc(2026, 4, 1), paid: true);
        IncomeRowResult nextYear = await CreateIncomeAsync(client, category, Utc(2027, 1, 2), paid: true);
        IncomeRowResult open = await CreateIncomeAsync(client, category, Utc(2026, 5, 1), paid: false);

        // Assert
        first.BookingNumber.ShouldBe("E-2026-0001");
        first.PaymentDate.ShouldBe(Utc(2026, 3, 1));
        second.BookingNumber.ShouldBe("E-2026-0002");
        nextYear.BookingNumber.ShouldBe("E-2027-0001");
        open.BookingNumber.ShouldBeNull();
        open.PaymentDate.ShouldBeNull();
    }

    [Fact]
    public async Task CreateIncomeAsync_Paid_UsesGivenPaymentDate()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        IncomeCategoryEntity category = await CreateCategoryAsync();

        // Act
        IncomeRowResult row = await CreateIncomeAsync(client, category, Utc(2026, 12, 28), paid: true, paymentDate: Utc(2027, 1, 4));

        // Assert
        row.PaymentDate.ShouldBe(Utc(2027, 1, 4));
        row.BookingNumber.ShouldBe("E-2027-0001");
    }

    [Fact]
    public async Task MarkAsPaidAsync_ReopenAndPayAgain_KeepsTheNumber()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        IncomeCategoryEntity category = await CreateCategoryAsync();
        IncomeRowResult created = await CreateIncomeAsync(client, category, Utc(2026, 3, 1), paid: false);

        // Act
        HttpResponseMessage pay = await client.PostAsJsonAsync($"/income/{created.Id}", new MarkPaidRequest { Paid = true, PaymentDate = Utc(2026, 3, 5) });
        HttpResponseMessage reopen = await client.PostAsJsonAsync($"/income/{created.Id}", new MarkPaidRequest { Paid = false });
        IncomeEntity reopened = await _db.Incomes.AsNoTracking().SingleAsync();
        HttpResponseMessage payAgain = await client.PostAsJsonAsync($"/income/{created.Id}", new MarkPaidRequest { Paid = true, PaymentDate = Utc(2026, 4, 10) });

        // Assert
        pay.StatusCode.ShouldBe(HttpStatusCode.OK);
        reopen.StatusCode.ShouldBe(HttpStatusCode.OK);
        payAgain.StatusCode.ShouldBe(HttpStatusCode.OK);
        reopened.PaymentDate.ShouldBeNull();
        BookingNumberEntity number = await _db.BookingNumbers.AsNoTracking().SingleAsync();
        number.Number.ShouldBe("E-2026-0001");
        IncomeEntity stored = await _db.Incomes.AsNoTracking().SingleAsync();
        stored.PaymentDate.ShouldBe(Utc(2026, 4, 10));
    }

    [Fact]
    public async Task MarkAsPaidAsync_PayAgainInAnotherYear_ReturnsBadRequestAndKeepsIncomeOpen()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        IncomeCategoryEntity category = await CreateCategoryAsync();
        IncomeRowResult created = await CreateIncomeAsync(client, category, Utc(2026, 3, 1), paid: true);
        await client.PostAsJsonAsync($"/income/{created.Id}", new MarkPaidRequest { Paid = false });

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync($"/income/{created.Id}", new MarkPaidRequest { Paid = true, PaymentDate = Utc(2027, 1, 2) });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        IncomeEntity stored = await _db.Incomes.AsNoTracking().SingleAsync();
        stored.Paid.ShouldBeNull();
        stored.PaymentDate.ShouldBeNull();
        (await _db.BookingNumbers.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task MarkAsPaidAsync_Contribution_AssignsNumberAndShowsItInTheRows()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        ContributionEntity contribution = await CreateContributionAsync(Utc(2026, 1, 31));

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync($"/contributions/{contribution.Id}", new MarkPaidRequest { Paid = true, PaymentDate = Utc(2026, 2, 3) });
        HttpResponseMessage rowsResponse = await client.GetAsync("/income");
        IncomeRowsResult? rows = await rowsResponse.Content.ReadFromJsonAsync<IncomeRowsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        rows.ShouldNotBeNull();
        IncomeRowResult row = rows.Items.Single(x => x.Id == contribution.Id);
        row.BookingNumber.ShouldBe("E-2026-0001");
        row.PaymentDate.ShouldBe(Utc(2026, 2, 3));
        row.DueDate.ShouldBe(Utc(2026, 1, 31));
    }

    [Fact]
    public async Task MarkAsPaidAsync_Receipt_UsesSeparateExpenseSequence()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        IncomeCategoryEntity category = await CreateCategoryAsync();
        IncomeRowResult income = await CreateIncomeAsync(client, category, Utc(2026, 3, 1), paid: true);
        ReceiptEntity receipt = new()
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Amount = 30m,
            ReceiptDate = Utc(2026, 3, 2),
            PaymentMethod = ReceiptPaymentMethod.CASH
        };
        await _db.Receipts.AddAsync(receipt);
        await _db.SaveChangesAsync();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync($"/receipts/{receipt.Id}/pay",
            new MarkReceiptPaidRequest { PaymentDate = Utc(2026, 3, 9) });
        ReceiptResult? result = await client.GetFromJsonAsync<ReceiptResult>($"/receipts/{receipt.Id}", _jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        income.BookingNumber.ShouldBe("E-2026-0001");
        result.ShouldNotBeNull();
        result.BookingNumber.ShouldBe("A-2026-0001");
        result.PaymentDate.ShouldBe(Utc(2026, 3, 9));
    }

    [Fact]
    public async Task AssignMissingAsync_NumbersExistingPaidEntriesByPaymentDate()
    {
        // Arrange
        IncomeCategoryEntity category = await CreateCategoryAsync();
        Guid later = await AddPaidIncomeAsync(category, Utc(2026, 6, 1));
        Guid earlier = await AddPaidIncomeAsync(category, Utc(2026, 2, 1));

        // Act
        await GetService<UniVerein.Api.Services.BookingNumberService>().AssignMissingAsync();

        // Assert
        Dictionary<Guid, string> numbers = await GetService<UniVerein.Api.Services.BookingNumberService>()
            .GetIncomeNumbersAsync(new[] { later, earlier });
        numbers[earlier].ShouldBe("E-2026-0001");
        numbers[later].ShouldBe("E-2026-0002");
    }

    private static DateTime Utc(int year, int month, int day) => new(year, month, day, 0, 0, 0, DateTimeKind.Utc);

    private async Task<IncomeCategoryEntity> CreateCategoryAsync()
    {
        IncomeCategoryEntity category = new() { Id = Guid.NewGuid(), Name = "Spenden" };
        await _db.IncomeCategories.AddAsync(category);
        await _db.SaveChangesAsync();
        return category;
    }

    private async Task<Guid> AddPaidIncomeAsync(IncomeCategoryEntity category, DateTime paymentDate)
    {
        IncomeEntity income = new()
        {
            Id = Guid.NewGuid(),
            CategoryId = category.Id,
            Payer = "Spender",
            Amount = 10,
            IncomeDate = paymentDate,
            Paid = DateTimeOffset.UtcNow,
            PaymentDate = paymentDate
        };
        await _db.Incomes.AddAsync(income);
        await _db.SaveChangesAsync();
        return income.Id;
    }

    private async Task<ContributionEntity> CreateContributionAsync(DateTime dueDate)
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
            MembershipStatus = MembershipStatus.MEMBER,
            MemberNumber = 7
        };
        ContributionEntity contribution = new()
        {
            Id = Guid.NewGuid(),
            MemberId = member.Id,
            MemberEntity = member,
            Amount = 12,
            DueDate = dueDate
        };
        await _db.Members.AddAsync(member);
        await _db.Contributions.AddAsync(contribution);
        await _db.SaveChangesAsync();
        return contribution;
    }

    private async Task<IncomeRowResult> CreateIncomeAsync(HttpClient client, IncomeCategoryEntity category, DateTime incomeDate,
        bool paid, DateTime? paymentDate = null)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/income", new IncomeRequest
        {
            CategoryId = category.Id,
            Payer = "Familie Krause",
            Amount = 50,
            IncomeDate = incomeDate,
            Paid = paid,
            PaymentDate = paymentDate
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        IncomeRowResult? row = await response.Content.ReadFromJsonAsync<IncomeRowResult>(_jsonSerializerOptions);
        row.ShouldNotBeNull();
        return row;
    }
}
