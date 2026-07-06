using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Exceptions;
using UniVerein.Api.Query;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using UniVerein.IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class ContributionsControllerTests : IntegrationTestBase
{
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public ContributionsControllerTests(UniVereinWebApplicationFactory factory)
        : base(factory)
    {
        _jsonSerializerOptions = new()
        {
            Converters = { new JsonStringEnumConverter() }
        };
    }

    public override async Task InitializeAsync()
    {
        await WithDbContext(async db =>
        {
            db.Members.RemoveRange(db.Members.AsQueryable());
            db.Contributions.RemoveRange(db.Contributions.AsQueryable());
            await db.ForceSaveChangesAsync();
        });
    }

    public override async Task DisposeAsync()
    {
        await WithDbContext(async db =>
        {
            db.Members.RemoveRange(db.Members.AsQueryable());
            db.Contributions.RemoveRange(db.Contributions.AsQueryable());
            await db.ForceSaveChangesAsync();
        });
    }

    // ---------------------------------------------------------------
    // GET /api/contributions/info
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetContributionInfo_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/contributions/info");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task GetContributionInfo_Authorized(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);
        List<(MemberEntity, ContributionEntity)> members = new();
        foreach (var index in Enumerable.Range(0, 5))
            members.Add(await CreateContributionEntity(index.ToString(), index.ToString()));
        decimal amount = members.Select(x => x.Item2.Amount).Sum();

        // Act
        HttpResponseMessage response = await client.GetAsync("/contributions/info");
        ContributionInfoResult? result =
            await response.Content.ReadFromJsonAsync<ContributionInfoResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result!.OpenPayments.ShouldBe(members.Count);
        result.OpenAmount.ShouldBe(amount);
    }

    // ---------------------------------------------------------------
    // GET /api/contributions
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetAllContributions_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/contributions");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task GetAllContributions_Authorized(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);
        List<(MemberEntity, ContributionEntity)> members = new();
        foreach (var index in Enumerable.Range(0, 5))
            members.Add(await CreateContributionEntity(index.ToString(), index.ToString()));

        // Act
        HttpResponseMessage response = await client.GetAsync("/contributions");
        AllContributionsResult? results =
            await response.Content.ReadFromJsonAsync<AllContributionsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        results.ShouldNotBeNull();
        results!.Total.ShouldBe(5);
        foreach ((MemberEntity member, ContributionEntity contribution) in members)
        {
            ContributionResult? result = results.Items.Find(x => x.Id == contribution.Id);
            result.ShouldNotBeNull();
            result!.Name.ShouldBe($"{member.FirstName} {member.LastName}");
            result.Amount.ShouldBe(contribution.Amount);
            result.Paid.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task GetAllContributions_paging_Success()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.USER);
        List<(MemberEntity, ContributionEntity)> members = new();
        foreach (var index in Enumerable.Range(0, 5))
            members.Add(await CreateContributionEntity(index.ToString(), index.ToString()));

        ContributionsQuery contributionsQuery = new()
        {
            Offset = 1,
            Limit = 1,
        };

        // Act
        HttpResponseMessage response = await client.GetAsync($"/contributions{contributionsQuery.GetQueryString()}");
        AllContributionsResult? results =
            await response.Content.ReadFromJsonAsync<AllContributionsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        results.ShouldNotBeNull();
        results!.Total.ShouldBe(5);
        results.Items.Count.ShouldBe(1);
        ContributionResult? result = results.Items.FirstOrDefault();
        result.ShouldNotBeNull();

        members.First(x => x.Item1.FirstName == "1").Item2.Id.ShouldBe(result!.Id);
    }

    [Theory]
    [InlineData(null, "test")]
    [InlineData(false, null)]
    public async Task GetAllContributions_byQueryParameter_Success(bool? unpaid, string? name)
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.USER);
        foreach (var index in Enumerable.Range(0, 5))
            await CreateContributionEntity(index.ToString(), index.ToString());
        (MemberEntity _, ContributionEntity contribution) = await CreateContributionEntity("test", "test", true);

        ContributionsQuery contributionsQuery = new()
        {
            Unpaid = unpaid,
            Name = name,
        };

        // Act
        HttpResponseMessage response = await client.GetAsync($"/contributions{contributionsQuery.GetQueryString()}");
        AllContributionsResult? results =
            await response.Content.ReadFromJsonAsync<AllContributionsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        results.ShouldNotBeNull();
        results!.Total.ShouldBe(1);
        results.Items.Count.ShouldBe(1);
        ContributionResult? result = results.Items.FirstOrDefault();
        result.ShouldNotBeNull();

        result!.Id.ShouldBe(contribution.Id);
    }

    // ---------------------------------------------------------------
    // PAID /api/{id}/contributions
    // ---------------------------------------------------------------

    [Fact]
    public async Task MarkAsPaid_WithoutToken_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response =
            await client.PostAsJsonAsync($"/contributions/{Guid.NewGuid()}?paid=true", new { });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MarkAsPaid_Forbidden()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.USER);

        // Act
        HttpResponseMessage response =
            await client.PostAsJsonAsync($"/contributions/{Guid.NewGuid()}?paid=true", new { });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task MarkAsPaid_NotFound(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response =
            await client.PostAsJsonAsync($"/contributions/{Guid.NewGuid()}?paid=true", new { });
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.ShouldNotBeNull();
        result!.StatusCode.ShouldBe((int)HttpStatusCode.NotFound);
        result.ErrorCode.ShouldBe(ApiErrorCodes.RESOURCE_NOT_FOUND);
        result.ErrorMessage.ShouldBe("Contribution config not found.");
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task MarkAsPaid_Success(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);
        (MemberEntity _, ContributionEntity contribution) = await CreateContributionEntity();

        // Act
        HttpResponseMessage response =
            await client.PostAsJsonAsync($"/contributions/{contribution.Id}?paid=true", new { });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await WithDbContext(async db =>
        {
            ContributionEntity? contributionEntity = await db.Contributions.FindAsync(contribution.Id);
            contributionEntity.ShouldNotBeNull();
            contributionEntity!.Paid.ShouldNotBeNull();
        });
    }

    // ---------------------------------------------------------------
    // Helper functions
    // ---------------------------------------------------------------

    private async Task<(MemberEntity, ContributionEntity)> CreateContributionEntity(string? firstName = null,
        string? lastName = null, bool? paid = null)
    {
        Guid memberId = Guid.NewGuid();
        MemberEntity memberEntity = new()
        {
            MandateId = memberId.ToString(),
            Id = memberId,
            FirstName = firstName ?? Guid.NewGuid().ToString(),
            LastName = lastName ?? Guid.NewGuid().ToString(),
            BirthdayEncrypted = string.Empty,
            City =  "Musterstadt",
            PostalCode = "12345",
            CountryCode = "DE",
            StreetEncrypted =  string.Empty
        };

        ContributionEntity contributionEntity = new()
        {
            Id = Guid.NewGuid(),
            MemberId = memberEntity.Id,
            MemberEntity = memberEntity,
            Amount = Random.Shared.Next(12, 1000),
            DueDate = DateTime.Now,
            Paid = paid == true ? DateTimeOffset.UtcNow : null
        };

        await WithDbContext(async db =>
        {
            await db.Members.AddAsync(memberEntity);
            await db.Contributions.AddAsync(contributionEntity);
            await db.SaveChangesAsync();
        });

        return (memberEntity, contributionEntity);
    }
}