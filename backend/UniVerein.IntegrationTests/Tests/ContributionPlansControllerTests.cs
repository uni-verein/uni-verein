using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using UniVerein.IntegrationTests.Infrastructure;
using Shouldly;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class ContributionPlansControllerTests : IntegrationTestBase
{
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public ContributionPlansControllerTests(UniVereinWebApplicationFactory factory)
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
            db.ContributionPlans.RemoveRange(db.ContributionPlans.AsQueryable());
            db.Members.RemoveRange(db.Members.AsQueryable());
            await db.ForceSaveChangesAsync();
        });
    }

    public override async Task DisposeAsync()
    {
        await WithDbContext(async db =>
        {
            db.ContributionPlans.RemoveRange(db.ContributionPlans.AsQueryable());
            db.Members.RemoveRange(db.Members.AsQueryable());
            await db.ForceSaveChangesAsync();
        });
    }

    // ---------------------------------------------------------------
    // GET /api/contributions
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetContributionPlan_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/contribution-plans");

        // AssertF
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task GetContributionPlan_WithExpiredToken_Unauthorized(UserRole role)
    {
        // Arrange
        IConfiguration configuration = Factory.Services.GetRequiredService<IConfiguration>();
        var expiredToken = JwtTestHelper.CreateToken(
            configuration,
            userId: Guid.NewGuid(),
            username: "expired",
            role: role,
            lifetime: TimeSpan.FromMinutes(-5));

        HttpClient client = CreateClient().WithBearerToken(expiredToken);

        // Act
        HttpResponseMessage response = await client.GetAsync("/contribution-plans");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task GetContributionPlan_Ok(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response = await client.GetAsync("/contribution-plans");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task GetContributionPlan_Success(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);
        List<ContributionPlanEntity> contributionPlans = new();
        foreach (var index in Enumerable.Range(0, 5))
            contributionPlans.Add(await CreateContributionPlanEntity(name: index.ToString()));

        // Act
        HttpResponseMessage response = await client.GetAsync("/contribution-plans");
        ContributionPlanResults? results =
            await response.Content.ReadFromJsonAsync<ContributionPlanResults>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        results.ShouldNotBeNull();
        results.Total.ShouldBe(5);
        foreach (ContributionPlanEntity contributionPlan in contributionPlans)
        {
            ContributionPlanResult? result = results.Items.FirstOrDefault(x => x.Id == contributionPlan.Id);
            result.ShouldNotBeNull();
            CompareContributionPlan(contributionPlan, result);
        }

        results.Items.Select(x => int.Parse(x.Name)).ToArray().ShouldBeEquivalentTo(Enumerable.Range(0, 5).ToArray());
    }

    // ---------------------------------------------------------------
    // CREATE /api/contributions
    // ---------------------------------------------------------------

    [Fact]
    public async Task CreateContributionPlan_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response =
            await client.PostAsJsonAsync("/contribution-plans", CreateContributionPlanRequest());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task CreateContributionPlan_WithExpiredToken_Unauthorized(UserRole role)
    {
        // Arrange
        IConfiguration configuration = Factory.Services.GetRequiredService<IConfiguration>();
        var expiredToken = JwtTestHelper.CreateToken(
            configuration,
            userId: Guid.NewGuid(),
            username: "expired",
            role: role,
            lifetime: TimeSpan.FromMinutes(-5));

        HttpClient client = CreateClient().WithBearerToken(expiredToken);

        // Act
        HttpResponseMessage response =
            await client.PostAsJsonAsync("/contribution-plans", CreateContributionPlanRequest());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task CreateContributionPlan_Forbidden(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response =
            await client.PostAsJsonAsync("/contribution-plans", CreateContributionPlanRequest());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateContributionPlan_NameIncorrect_BadRequest()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        ContributionPlanRequest contributionPlan =
            CreateContributionPlanRequest(name: "testtesttesttesttesttesttesttesttesttesttesttesttest");

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/contribution-plans", contributionPlan);
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        result.ShouldNotBeNull();
        result!.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
        result!.MoreInfo.ShouldBe("Name length must be less long then 51 characters.");
    }

    [Fact]
    public async Task CreateContributionPlan_AmountIncorrect_BadRequest()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        ContributionPlanRequest contributionPlan = CreateContributionPlanRequest(amount: -1);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/contribution-plans", contributionPlan);
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        result.ShouldNotBeNull();
        result!.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
        result!.MoreInfo.ShouldBe("Amount must be greater then 0.");
    }

    [Fact]
    public async Task CreateContributionPlan_WithDuplicateNameAmount_Conflict()
    {
        HttpClient client = CreateAdminClient();
        ContributionPlanEntity contributionPlanEntity = await CreateContributionPlanEntity();
        ContributionPlanRequest contributionPlanRequest =
            CreateContributionPlanRequest(contributionPlanEntity.Name, contributionPlanEntity.Amount);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/contribution-plans", contributionPlanRequest);
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        result.ShouldNotBeNull();
        result!.StatusCode.ShouldBe((int)HttpStatusCode.Conflict);
        result!.MoreInfo.ShouldBe("Contribution plan with same name and amount already exists. Try a other name or amount.");
    }

    [Fact]
    public async Task CreateContribution_WithValidData_ReturnsCreated()
    {
        // Arrange
        HttpClient client = CreateAdminClient();
        ContributionPlanRequest payload = CreateContributionPlanRequest();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/contribution-plans", payload);
        ContributionPlanResult? result =
            await response.Content.ReadFromJsonAsync<ContributionPlanResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        result.ShouldNotBeNull();
        await WithDbContext(async db =>
        {
            ContributionPlanEntity? contributionPlan = await db.ContributionPlans.FindAsync(result!.Id);
            contributionPlan.ShouldNotBeNull();
            CompareContributionPlan(contributionPlan!, result);
        });
    }

    // ---------------------------------------------------------------
    // UPDATE /api/contribution-plan/{id}
    // ---------------------------------------------------------------

    [Fact]
    public async Task UpdateContributionPlan_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync($"/contribution-plans/{Guid.NewGuid()}",
            CreateContributionPlanUpdateRequest());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task UpdateContributionPlan_WithExpiredToken_Unauthorized(UserRole role)
    {
        // Arrange
        IConfiguration configuration = Factory.Services.GetRequiredService<IConfiguration>();
        var expiredToken = JwtTestHelper.CreateToken(
            configuration,
            userId: Guid.NewGuid(),
            username: "expired",
            role: role,
            lifetime: TimeSpan.FromMinutes(-5));

        HttpClient client = CreateClient().WithBearerToken(expiredToken);

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync($"/contribution-plans/{Guid.NewGuid()}",
            CreateContributionPlanUpdateRequest());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task UpdateContributionPlan_Forbidden(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync($"/contribution-plans/{Guid.NewGuid()}",
            CreateContributionPlanUpdateRequest());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateContributionPlan_Empty_BadRequest()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync($"/contribution-plans/{Guid.NewGuid()}", new { });
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        result.ShouldNotBeNull();
        result!.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
        result!.MoreInfo.ShouldBe("Nothing to update.");
    }

    [Fact]
    public async Task UpdateContributionPlan_NameIncorrect_BadRequest()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        ContributionPlanUpdateRequest contributionPlan =
            CreateContributionPlanUpdateRequest(name: "testtesttesttesttesttesttesttesttesttesttesttesttest");

        // Act
        HttpResponseMessage response =
            await client.PatchAsJsonAsync($"/contribution-plans/{Guid.NewGuid()}", contributionPlan);
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        result.ShouldNotBeNull();
        result!.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
        result!.MoreInfo.ShouldBe("Name length must be less long then 51 characters.");
    }

    [Fact]
    public async Task UpdateContributionPlan_AmountIncorrect_BadRequest()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        ContributionPlanUpdateRequest contributionPlan = CreateContributionPlanUpdateRequest(amount: -1);

        // Act
        HttpResponseMessage response =
            await client.PatchAsJsonAsync($"/contribution-plans/{Guid.NewGuid()}", contributionPlan);
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        result.ShouldNotBeNull();
        result!.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
        result!.MoreInfo.ShouldBe("Amount must be greater then 0.");
    }

    [Fact]
    public async Task UpdateContributionPlan_NotFound()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        ContributionPlanUpdateRequest contributionPlan = CreateContributionPlanUpdateRequest();

        // Act
        HttpResponseMessage response =
            await client.PatchAsJsonAsync($"/contribution-plans/{Guid.NewGuid()}", contributionPlan);
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.ShouldNotBeNull();
        result!.StatusCode.ShouldBe((int)HttpStatusCode.NotFound);
        result!.MoreInfo.ShouldBe("Contribution plan with given ID not found. Try a other contribution plan ID.");
    }

    [Fact]
    public async Task UpdateContributionPlan_WithDuplicateNameAmount_Conflict()
    {
        HttpClient client = CreateAdminClient();
        ContributionPlanEntity contributionPlanEntity = await CreateContributionPlanEntity();
        ContributionPlanEntity testContributionPlanEntity = await CreateContributionPlanEntity();
        ContributionPlanUpdateRequest contributionPlanRequest =
            CreateContributionPlanUpdateRequest(contributionPlanEntity.Name, contributionPlanEntity.Amount);

        // Act
        HttpResponseMessage response =
            await client.PatchAsJsonAsync($"/contribution-plans/{testContributionPlanEntity.Id}",
                contributionPlanRequest);
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        result.ShouldNotBeNull();
        result!.StatusCode.ShouldBe((int)HttpStatusCode.Conflict);
        result!.MoreInfo.ShouldBe("Contribution plan with same name and amount already exists. Try a other name or amount.");
    }

    [Fact]
    public async Task UpdateContributionPlan_Success()
    {
        HttpClient client = CreateAdminClient();
        ContributionPlanEntity contributionPlanEntity = await CreateContributionPlanEntity();
        ContributionPlanUpdateRequest contributionPlanRequest = CreateContributionPlanUpdateRequest();

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync($"/contribution-plans/{contributionPlanEntity.Id}",
            contributionPlanRequest);
        ContributionPlanResult? result =
            await response.Content.ReadFromJsonAsync<ContributionPlanResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result!.Name.ShouldBe(contributionPlanRequest.Name);
        result!.Amount.ShouldBe((decimal)contributionPlanRequest.Amount!);
        result!.Interval.ShouldBe((Interval)contributionPlanRequest.Interval!);
        await WithDbContext(async db =>
        {
            ContributionPlanEntity? contributionPlan =
                await db.ContributionPlans.FirstOrDefaultAsync(x => x.Id == contributionPlanEntity.Id);
            contributionPlan.ShouldNotBeNull();
            CompareContributionPlan(contributionPlan!, result!);
        });
    }

    // ---------------------------------------------------------------
    // DELETE /api/contribution-plan/{id}
    // ---------------------------------------------------------------

    [Fact]
    public async Task DeleteContributionPlan_WithoutToken_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/contribution-plans/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task DeleteContributionPlan_Forbidden(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/contribution-plans/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteContributionPlan_NotFound()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/contribution-plans/{Guid.NewGuid()}");
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.ShouldNotBeNull();
        result!.StatusCode.ShouldBe((int)HttpStatusCode.NotFound);
        result!.ErrorMessage.ShouldBe("Contribution plan not found.");
    }

    [Fact]
    public async Task DeleteContributionPlan_BadRequest()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        ContributionPlanEntity contributionPlanEntity = await CreateContributionPlanEntity(createMember: true);

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/contribution-plans/{contributionPlanEntity.Id}");
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        result.ShouldNotBeNull();
        result!.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
        result!.ErrorMessage.ShouldBe("Failed request validation");
    }

    [Fact]
    public async Task DeleteContributionPlan_AsAdmin_ReturnsOk()
    {
        HttpClient client = CreateAdminClient();
        ContributionPlanEntity contributionPlanEntity = await CreateContributionPlanEntity();

        // Act
        var response = await client.DeleteAsync($"/contribution-plans/{contributionPlanEntity.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ---------------------------------------------------------------
    // Helper functions
    // ---------------------------------------------------------------

    private async Task<ContributionPlanEntity> CreateContributionPlanEntity(Guid? id = null, string? name = null,
        bool? createMember = null)
    {
        ContributionPlanEntity contributionPlanEntity = new()
        {
            Id = id ?? Guid.NewGuid(),
            Name = name ?? Guid.NewGuid().ToString(),
            Amount = 20,
            Interval = Interval.MONTHLY
        };

        await WithDbContext(async db =>
        {
            await db.ContributionPlans.AddAsync(contributionPlanEntity);
            Guid memberId = Guid.NewGuid();
            if (createMember == true)
                await db.Members.AddAsync(new MemberEntity()
                {
                    MandateId = memberId.ToString(),
                    Id = memberId,
                    FirstName = Guid.NewGuid().ToString(),
                    LastName = Guid.NewGuid().ToString(),
                    BirthdayEncrypted = string.Empty,
                    ContributionPlan = contributionPlanEntity,
                    ContributionPlanId = contributionPlanEntity.Id,
                    City = string.Empty,
                    CountryCode = string.Empty,
                    StreetEncrypted = string.Empty,
                    PostalCode = string.Empty,
                });

            await db.SaveChangesAsync();
        });

        return contributionPlanEntity;
    }

    private void CompareContributionPlan(ContributionPlanEntity entity, ContributionPlanResult result)
    {
        entity.Id.ShouldBe(result.Id);
        entity.Name.ShouldBe(result.Name);
        entity.Amount.ShouldBe(result.Amount);
        entity.Interval.ShouldBe(result.Interval);
    }

    private ContributionPlanRequest CreateContributionPlanRequest(string? name = null, decimal? amount = null)
    {
        return new ContributionPlanRequest()
        {
            Name = name ?? Guid.NewGuid().ToString(),
            Amount = amount ?? 21,
            Interval = Interval.MONTHLY
        };
    }

    private ContributionPlanUpdateRequest CreateContributionPlanUpdateRequest(string? name = null,
        decimal? amount = null)
    {
        return new()
        {
            Name = name ?? Guid.NewGuid().ToString(),
            Amount = amount ?? 25,
            Interval = Interval.YEARLY
        };
    }
}