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
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class LinkControllerTests : IntegrationTestBase
{
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public LinkControllerTests(UniVereinWebApplicationFactory factory) : base(factory)
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
            db.LinkSettings.RemoveRange(db.LinkSettings.AsQueryable());
            await db.ForceSaveChangesAsync();
        });
    }

    public override async Task DisposeAsync()
    {
        await WithDbContext(async db =>
        {
            db.LinkSettings.RemoveRange(db.LinkSettings.AsQueryable());
            await db.ForceSaveChangesAsync();
        });
    }

    // ---------------------------------------------------------------
    // GET /api/link
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetLinkConfig_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/link");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task GetLinkConfig_Authorized(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);
        await CreateLinkSettingsEntity();

        // Act
        HttpResponseMessage response = await client.GetAsync("/link");
        AllLinkSettingsResults? results =
            await response.Content.ReadFromJsonAsync<AllLinkSettingsResults>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        results.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetLinkConfig_EmptyList()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.USER);

        // Act
        HttpResponseMessage response = await client.GetAsync("/link");
        AllLinkSettingsResults? results =
            await response.Content.ReadFromJsonAsync<AllLinkSettingsResults>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        results.ShouldNotBeNull();
        results!.Items.ShouldBeEmpty();
        results.Total.ShouldBe(0);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task GetLinkConfig_Success(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);
        LinkSettingsEntity linkSettingsEntity = await CreateLinkSettingsEntity();

        // Act
        HttpResponseMessage response = await client.GetAsync("/link");
        AllLinkSettingsResults? results =
            await response.Content.ReadFromJsonAsync<AllLinkSettingsResults>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        results.ShouldNotBeNull();
        results!.Items.ShouldNotBeEmpty();
        results.Total.ShouldBe(1);
        results.Items[0].Id.ShouldBe(linkSettingsEntity.Id);
        results.Items[0].Link.ShouldBe(linkSettingsEntity.Link);
        results.Items[0].Name.ShouldBe(linkSettingsEntity.Name);
        results.Items[0].Icon.ShouldBe(linkSettingsEntity.Icon);
    }

    // ---------------------------------------------------------------
    // CREATE /api/link
    // ---------------------------------------------------------------

    [Fact]
    public async Task CreateLinkConfig_WithoutToken_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/link", CreateLinkConfigRequest());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task CreateLinkConfig_Forbidden(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/link", CreateLinkConfigRequest());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateLinkConfig_BadRequest()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/link", CreateLinkConfigRequest(" "));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateLinkConfig_linkToLong_BadRequest()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/link",
            CreateLinkConfigRequest(
                "ffffffffffaaaaaaaaaaffffffffffaaaaaaaaaaffffffffffaaaaaaaaaaffffffffffaaaaaaaaaaffffffffffaaaaaaaaaa1"));
        ErrorDetailsResult? results =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        results.ShouldNotBeNull();
        results!.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
        results.MoreInfo.ShouldBe("The link contains too many characters. The maximum length is 100.");
    }

    [Fact]
    public async Task CreateLinkConfig_Success()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        LinkSettingsRequest request = CreateLinkConfigRequest();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/link", request);
        LinkSettingsResult? results =
            await response.Content.ReadFromJsonAsync<LinkSettingsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        results.ShouldNotBeNull();
        await WithDbContext(async db =>
        {
            LinkSettingsEntity? linkSettingsEntity = await db.LinkSettings.FirstOrDefaultAsync();
            linkSettingsEntity.ShouldNotBeNull();
            linkSettingsEntity!.Link.ShouldBe(request.Link);
            linkSettingsEntity.Name.ShouldBe(request.Name);
            linkSettingsEntity.Icon.ShouldBe(request.Icon);
        });
    }

    // ---------------------------------------------------------------
    // UPDATE /api/link
    // ---------------------------------------------------------------

    [Fact]
    public async Task UpdateLinkConfig_Success()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        LinkSettingsEntity linkSettings = await CreateLinkSettingsEntity();
        LinkSettingsUpdateRequest request = CreateLinkConfigUpdateRequest();

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync($"/link/{linkSettings.Id}", request);
        LinkSettingsResult? results =
            await response.Content.ReadFromJsonAsync<LinkSettingsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        results.ShouldNotBeNull();
        await WithDbContext(async db =>
        {
            LinkSettingsEntity? linkSettingsEntity =
                await db.LinkSettings.FirstOrDefaultAsync(x => x.Id == linkSettings.Id);
            linkSettingsEntity.ShouldNotBeNull();
            linkSettingsEntity!.Link.ShouldBe(request.Link);
            linkSettingsEntity.Name.ShouldBe(request.Name);
            linkSettingsEntity.Icon.ShouldBe(request.Icon);
        });
    }

    [Fact]
    public async Task UpdateLinkConfig_NotFound()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        LinkSettingsRequest request = CreateLinkConfigRequest();

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync($"/link/{Guid.NewGuid()}", request);
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.ShouldNotBeNull();
        result!.StatusCode.ShouldBe((int)HttpStatusCode.NotFound);
        result.ErrorMessage.ShouldBe("Link not found.");
    }

    // ---------------------------------------------------------------
    // DELETE /api/link
    // ---------------------------------------------------------------

    [Fact]
    public async Task DeleteLinkConfig_WithoutToken_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/link/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task DeleteLinkConfig_Forbidden(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/link/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteLinkConfig_NotFound()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/link/{Guid.NewGuid()}");
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.ShouldNotBeNull();
        result!.StatusCode.ShouldBe((int)HttpStatusCode.NotFound);
        result.ErrorMessage.ShouldBe("Link not found.");
    }

    [Fact]
    public async Task DeleteLinkConfig_Success()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        LinkSettingsEntity linkConfig = await CreateLinkSettingsEntity();

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/link/{linkConfig.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await WithDbContext(async db =>
        {
            LinkSettingsEntity? result = await db.LinkSettings.FindAsync(linkConfig.Id);
            result.ShouldNotBeNull();
            result!.DeletedAt.ShouldNotBeNull();
        });
    }

    // ---------------------------------------------------------------
    // Helper functions
    // ---------------------------------------------------------------

    private LinkSettingsRequest CreateLinkConfigRequest(string? link = null, string? name = null)
    {
        return new()
        {
            Link = link ?? Guid.NewGuid().ToString(),
            Name = name ?? "Short link name",
            Icon = Guid.NewGuid().ToString()
        };
    }

    private LinkSettingsUpdateRequest CreateLinkConfigUpdateRequest(string? link = null, string? name = null)
    {
        return new()
        {
            Link = link ?? Guid.NewGuid().ToString(),
            Name = name ?? "This is a link name",
            Icon = Guid.NewGuid().ToString()
        };
    }

    private async Task<LinkSettingsEntity> CreateLinkSettingsEntity()
    {
        LinkSettingsEntity linkSettingsEntity = new()
        {
            Id = Guid.NewGuid(),
            Link = Guid.NewGuid().ToString(),
            Name = Guid.NewGuid().ToString(),
            Icon = Guid.NewGuid().ToString()
        };

        await WithDbContext(async db =>
        {
            await db.LinkSettings.AddAsync(linkSettingsEntity);
            await db.SaveChangesAsync();
        });

        return linkSettingsEntity;
    }
}