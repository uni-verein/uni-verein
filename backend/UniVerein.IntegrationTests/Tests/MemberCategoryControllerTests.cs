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

public class MemberCategoryControllerTests : IntegrationTestBase
{
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public MemberCategoryControllerTests(UniVereinWebApplicationFactory factory)
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
            db.MemberCategories.RemoveRange(db.MemberCategories.AsQueryable());
            db.Members.RemoveRange(db.Members.AsQueryable());
            await db.ForceSaveChangesAsync();
        });
    }

    public override async Task DisposeAsync()
    {
        await WithDbContext(async db =>
        {
            db.MemberCategories.RemoveRange(db.MemberCategories.AsQueryable());
            db.Members.RemoveRange(db.Members.AsQueryable());
            await db.ForceSaveChangesAsync();
        });
    }

    // ---------------------------------------------------------------
    // GET /api/member-categories
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetContributionPlan_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/member-categories");

        // Assert
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
        HttpResponseMessage response = await client.GetAsync("/member-categories");

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
        HttpResponseMessage response = await client.GetAsync("/member-categories");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task GetMemberCategory_Success(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);
        List<MemberCategoryEntity> memberCategories = new();
        foreach (var index in Enumerable.Range(0, 5))
            memberCategories.Add(await CreateMemberCategoryEntity(name: index.ToString()));

        // Act
        HttpResponseMessage response = await client.GetAsync("/member-categories");
        MemberCategoryResults? results =
            await response.Content.ReadFromJsonAsync<MemberCategoryResults>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        results.ShouldNotBeNull();
        results.Total.ShouldBe(5);
        foreach (MemberCategoryEntity memberCategory in memberCategories)
        {
            MemberCategoryResult? result = results.Items.FirstOrDefault(x => x.Id == memberCategory.Id);
            result.ShouldNotBeNull();
            CompareMemberCategory(memberCategory, result);
        }

        results.Items.Select(x => int.Parse(x.Name)).ToArray().ShouldBeEquivalentTo(Enumerable.Range(0, 5).ToArray());
    }

    // ---------------------------------------------------------------
    // CREATE /api/member-categories
    // ---------------------------------------------------------------

    [Fact]
    public async Task CreateMemberCategory_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response =
            await client.PostAsJsonAsync("/member-categories", CreateMemberCategoryRequest());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task CreateMemberCategory_WithExpiredToken_Unauthorized(UserRole role)
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
            await client.PostAsJsonAsync("/member-categories", CreateMemberCategoryRequest());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task CreateMemberCategory_Forbidden(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response =
            await client.PostAsJsonAsync("/member-categories", CreateMemberCategoryRequest());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateMemberCategory_NameIncorrect_BadRequest()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        MemberCategoryRequest memberCategory =
            CreateMemberCategoryRequest(name: "testtesttesttesttesttesttesttesttesttesttesttesttest");

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/member-categories", memberCategory);
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
        result.MoreInfo.ShouldBe("Name length must be less long then 51 characters.");
    }

    [Fact]
    public async Task CreateMemberCategory_CategoryIncorrect_BadRequest()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        MemberCategoryRequest memberCategory =
            CreateMemberCategoryRequest(category: "testtesttesttesttesttesttesttesttesttesttesttesttest");

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/member-categories", memberCategory);
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
        result.MoreInfo.ShouldBe("Category length must be less long then 51 characters.");
    }

    [Fact]
    public async Task CreateMemberCategory_WithDuplicateNameCategory_Conflict()
    {
        HttpClient client = CreateAdminClient();
        MemberCategoryEntity memberCategoryEntity = await CreateMemberCategoryEntity();
        MemberCategoryRequest contributionPlanRequest =
            CreateMemberCategoryRequest(memberCategoryEntity.Name, memberCategoryEntity.Category);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/member-categories", contributionPlanRequest);
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe((int)HttpStatusCode.Conflict);
        result.MoreInfo.ShouldBe("Member category with same name and category already exists. Try a other name and category.");
    }

    [Fact]
    public async Task CreateMemberCategory_WithValidData_ReturnsCreated()
    {
        // Arrange
        HttpClient client = CreateAdminClient();
        MemberCategoryRequest payload = CreateMemberCategoryRequest();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/member-categories", payload);
        MemberCategoryResult? result =
            await response.Content.ReadFromJsonAsync<MemberCategoryResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        result.ShouldNotBeNull();
        await WithDbContext(async db =>
        {
            MemberCategoryEntity? memberCategory = await db.MemberCategories.FindAsync(result.Id);
            memberCategory.ShouldNotBeNull();
            CompareMemberCategory(memberCategory, result);
        });
    }

    // ---------------------------------------------------------------
    // UPDATE /api/member-categories/{id}
    // ---------------------------------------------------------------

    [Fact]
    public async Task UpdateMemberCategory_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync($"/member-categories/{Guid.NewGuid()}",
            CreateMemberCategoryUpdateRequest());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task UpdateMemberCategory_WithExpiredToken_Unauthorized(UserRole role)
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
        HttpResponseMessage response = await client.PutAsJsonAsync($"/member-categories/{Guid.NewGuid()}",
            CreateMemberCategoryUpdateRequest());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task UpdateMemberCategory_Forbidden(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync($"/member-categories/{Guid.NewGuid()}",
            CreateMemberCategoryUpdateRequest());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateMemberCategory_NameIncorrect_BadRequest()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        MemberCategoryUpdateRequest memberCategory =
            CreateMemberCategoryUpdateRequest(name: "testtesttesttesttesttesttesttesttesttesttesttesttest");

        // Act
        HttpResponseMessage response =
            await client.PutAsJsonAsync($"/member-categories/{Guid.NewGuid()}", memberCategory);
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
        result.MoreInfo.ShouldBe("Name length must be less long then 51 characters.");
    }

    [Fact]
    public async Task UpdateMemberCategory_CategoryIncorrect_BadRequest()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        MemberCategoryUpdateRequest memberCategory =
            CreateMemberCategoryUpdateRequest(category: "testtesttesttesttesttesttesttesttesttesttesttesttest");

        // Act
        HttpResponseMessage response =
            await client.PutAsJsonAsync($"/member-categories/{Guid.NewGuid()}", memberCategory);
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
        result.MoreInfo.ShouldBe("Category length must be less long then 51 characters.");
    }

    [Fact]
    public async Task UpdateMemberCategory_NotFound()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        MemberCategoryUpdateRequest memberCategory = CreateMemberCategoryUpdateRequest();

        // Act
        HttpResponseMessage response =
            await client.PutAsJsonAsync($"/member-categories/{Guid.NewGuid()}", memberCategory);
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe((int)HttpStatusCode.NotFound);
        result.MoreInfo.ShouldBe("Member category with given ID not found. Try a other member category ID.");
    }

    [Fact]
    public async Task UpdateMemberCategory_WithDuplicateNameAmount_Conflict()
    {
        HttpClient client = CreateAdminClient();
        MemberCategoryEntity memberCategoryEntity = await CreateMemberCategoryEntity();
        MemberCategoryEntity testMemberCategoryEntity = await CreateMemberCategoryEntity();
        MemberCategoryUpdateRequest memberCategoryRequest =
            CreateMemberCategoryUpdateRequest(memberCategoryEntity.Name, memberCategoryEntity.Category);

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync($"/member-categories/{testMemberCategoryEntity.Id}",
            memberCategoryRequest);
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe((int)HttpStatusCode.Conflict);
        result.MoreInfo.ShouldBe("Member category with same name and category already exists. Try a other name and category.");
    }

    [Fact]
    public async Task UpdateMemberCategory_Success()
    {
        HttpClient client = CreateAdminClient();
        MemberCategoryEntity memberCategoryEntity = await CreateMemberCategoryEntity();
        MemberCategoryUpdateRequest memberCategoryRequest = CreateMemberCategoryUpdateRequest();

        // Act
        HttpResponseMessage response =
            await client.PutAsJsonAsync($"/member-categories/{memberCategoryEntity.Id}", memberCategoryRequest);
        MemberCategoryResult? result =
            await response.Content.ReadFromJsonAsync<MemberCategoryResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.Name.ShouldBe(memberCategoryRequest.Name);
        result.Category.ShouldBe(memberCategoryRequest.Category);
        await WithDbContext(async db =>
        {
            MemberCategoryEntity? memberCategory =
                await db.MemberCategories.FirstOrDefaultAsync(x => x.Id == memberCategoryEntity.Id);
            memberCategory.ShouldNotBeNull();
            CompareMemberCategory(memberCategory, result);
        });
    }

    // ---------------------------------------------------------------
    // DELETE /api/member-categories/{id}
    // ---------------------------------------------------------------

    [Fact]
    public async Task DeleteMemberCategory_WithoutToken_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/member-categories/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task DeleteMemberCategory_Forbidden(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/member-categories/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteMemberCategory_NotFound()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/member-categories/{Guid.NewGuid()}");
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe((int)HttpStatusCode.NotFound);
        result.ErrorMessage.ShouldBe("Member category not found.");
    }

    [Fact]
    public async Task DeleteMemberCategory_BadRequest()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        MemberCategoryEntity memberCategoryEntity = await CreateMemberCategoryEntity(createMember: true);

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/member-categories/{memberCategoryEntity.Id}");
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
        result.ErrorMessage.ShouldBe("Failed request validation");
    }

    [Fact]
    public async Task DeleteMemberCategory_AsAdmin_ReturnsOk()
    {
        HttpClient client = CreateAdminClient();
        MemberCategoryEntity memberCategoryEntity = await CreateMemberCategoryEntity();

        // Act
        var response = await client.DeleteAsync($"/member-categories/{memberCategoryEntity.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ---------------------------------------------------------------
    // Helper functions
    // ---------------------------------------------------------------

    private async Task<MemberCategoryEntity> CreateMemberCategoryEntity(Guid? id = null, string? name = null,
        string? category = null, bool? createMember = null)
    {
        MemberCategoryEntity memberCategoryEntity = new()
        {
            Id = id ?? Guid.NewGuid(),
            Name = name ?? Guid.NewGuid().ToString(),
            Category = category ?? Guid.NewGuid().ToString()
        };

        await WithDbContext(async db =>
        {
            await db.MemberCategories.AddAsync(memberCategoryEntity);
            Guid memberId = Guid.NewGuid();
            if (createMember == true)
                await db.Members.AddAsync(new MemberEntity()
                {
                    MandateId = memberId.ToString(),
                    Id = memberId,
                    FirstName = Guid.NewGuid().ToString(),
                    LastName = Guid.NewGuid().ToString(),
                    BirthdayEncrypted = string.Empty,
                    MemberCategory = memberCategoryEntity,
                    MemberCategoryId = memberCategoryEntity.Id,
                    StreetEncrypted = string.Empty,
                    City = string.Empty,
                    PostalCode = "12345",
                    CountryCode = "DE"
                });

            await db.SaveChangesAsync();
        });

        return memberCategoryEntity;
    }

    private void CompareMemberCategory(MemberCategoryEntity entity, MemberCategoryResult result)
    {
        entity.Id.ShouldBe(result.Id);
        entity.Name.ShouldBe(result.Name);
        entity.Category.ShouldBe(result.Category);
    }

    private MemberCategoryRequest CreateMemberCategoryRequest(string? name = null, string? category = null)
    {
        return new()
        {
            Name = name ?? Guid.NewGuid().ToString(),
            Category = category ?? Guid.NewGuid().ToString()
        };
    }

    private MemberCategoryUpdateRequest CreateMemberCategoryUpdateRequest(string? name = null, string? category = null)
    {
        return new()
        {
            Name = name ?? Guid.NewGuid().ToString(),
            Category = category ?? Guid.NewGuid().ToString()
        };
    }
}