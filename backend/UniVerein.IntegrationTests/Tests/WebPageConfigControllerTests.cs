using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Services;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using UniVerein.IntegrationTests.Infrastructure;
using Shouldly;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class WebPageConfigControllerTests : IntegrationTestBase
{
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public WebPageConfigControllerTests(UniVereinWebApplicationFactory factory)
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
            db.WebPageConfigs.RemoveRange(db.WebPageConfigs.AsQueryable());
            db.CreditorConfigs.RemoveRange(db.CreditorConfigs.AsQueryable());
            db.MailSettings.RemoveRange(db.MailSettings.AsQueryable());
            db.LinkSettings.RemoveRange(db.LinkSettings.AsQueryable());
            await db.ForceSaveChangesAsync();
        });
    }

    public override async Task DisposeAsync()
    {
        await WithDbContext(async db =>
        {
            db.WebPageConfigs.RemoveRange(db.WebPageConfigs.AsQueryable());
            db.CreditorConfigs.RemoveRange(db.CreditorConfigs.AsQueryable());
            db.MailSettings.RemoveRange(db.MailSettings.AsQueryable());
            db.LinkSettings.RemoveRange(db.LinkSettings.AsQueryable());
            await db.ForceSaveChangesAsync();
        });
    }

    // ---------------------------------------------------------------
    // GET /api/web-page-config
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetWebPageConfig_NotConfigured_NotFound()
    {
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/web-page-config");
        ErrorDetailsResult? results =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        results.ShouldNotBeNull();
        results!.StatusCode.ShouldBe((int)HttpStatusCode.NotFound);
        results.ErrorMessage.ShouldBe("Web page config not found.");
    }

    [Fact]
    public async Task GetWebPageConfig_WithoutLogin_Success()
    {
        HttpClient client = CreateClient();
        WebPageConfigEntity webPageConfigEntity = await CreateWebPageConfigEntity();

        // Act
        HttpResponseMessage response = await client.GetAsync("/web-page-config");
        WebPageConfigResult? results =
            await response.Content.ReadFromJsonAsync<WebPageConfigResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        results.ShouldNotBeNull();
        results!.Id.ShouldBe(webPageConfigEntity.Id);
        results.PageName.ShouldBe(webPageConfigEntity.PageName);
        results.Logo.ShouldBe(webPageConfigEntity.Logo);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task GetWebPageConfig_Success(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);
        WebPageConfigEntity webPageConfigEntity = await CreateWebPageConfigEntity();

        // Act
        HttpResponseMessage response = await client.GetAsync("/web-page-config");
        WebPageConfigResult? results =
            await response.Content.ReadFromJsonAsync<WebPageConfigResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        results.ShouldNotBeNull();
        results!.Id.ShouldBe(webPageConfigEntity.Id);
        results.PageName.ShouldBe(webPageConfigEntity.PageName);
        results.Logo.ShouldBe(webPageConfigEntity.Logo);
    }

    // ---------------------------------------------------------------
    // CREATE/UPDATE /api/web-page-config
    // ---------------------------------------------------------------

    [Fact]
    public async Task CreateWebPageConfig_WithoutToken_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync("/web-page-config", CreateWebPageConfigRequest());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task CreateWebPageConfig_Forbidden(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync("/web-page-config", CreateWebPageConfigRequest());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateWebPageConfig_BadRequest()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync("/web-page-config", CreateWebPageConfigRequest(" "));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CreateWebPageConfig_Success()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        WebPageConfigRequest request = CreateWebPageConfigRequest();

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync("/web-page-config", request);
        WebPageConfigResult? results =
            await response.Content.ReadFromJsonAsync<WebPageConfigResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        results.ShouldNotBeNull();
        await WithDbContext(async db =>
        {
            WebPageConfigEntity? webPageConfigEntity = await db.WebPageConfigs.FirstOrDefaultAsync();
            webPageConfigEntity.ShouldNotBeNull();
            webPageConfigEntity!.PageName.ShouldBe(request.PageName);
        });
    }

    [Fact]
    public async Task UpdateSoftDeletedWebPageConfig_Success()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        await CreateWebPageConfigEntity(true);
        WebPageConfigRequest request = CreateWebPageConfigRequest();

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync("/web-page-config", request);
        WebPageConfigResult? results =
            await response.Content.ReadFromJsonAsync<WebPageConfigResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        results.ShouldNotBeNull();
        await WithDbContext(async db =>
        {
            WebPageConfigEntity? webPageConfigEntity = await db.WebPageConfigs.FirstOrDefaultAsync();
            webPageConfigEntity.ShouldNotBeNull();
            webPageConfigEntity!.PageName.ShouldBe(request.PageName);
        });
    }

    // ---------------------------------------------------------------
    // DELETE /api/web-page-config
    // ---------------------------------------------------------------

    [Fact]
    public async Task DeleteWebPageConfig_WithoutToken_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/web-page-config/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task DeleteWebPageConfig_Forbidden(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/web-page-config/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteWebPageConfig_NotFound()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/web-page-config/{Guid.NewGuid()}");
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.ShouldNotBeNull();
        result!.StatusCode.ShouldBe((int)HttpStatusCode.NotFound);
        result.ErrorMessage.ShouldBe("Web page config not found.");
    }

    [Fact]
    public async Task DeleteWebPageConfig_Success()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        WebPageConfigEntity webPageConfig = await CreateWebPageConfigEntity();

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/web-page-config/{webPageConfig.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await WithDbContext(async db =>
        {
            WebPageConfigEntity? result = await db.WebPageConfigs.FindAsync(webPageConfig.Id);
            result.ShouldNotBeNull();
            result!.DeletedAt.ShouldNotBeNull();
        });
    }

    // ---------------------------------------------------------------
    // GET /api/web-page-config/sidebar
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetSidebarSettings_WithoutToken_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync($"/web-page-config/sidebar");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    [InlineData(UserRole.ADMIN)]
    public async Task GetSidebarSettings_Success(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);
        await CreateCreditorConfigEntity();
        await CreateMailSettingsEntity();
        LinkSettingsEntity linkSettingsEntity = await CreateDataLinkSettingsEntity();

        // Act
        HttpResponseMessage response = await client.GetAsync($"/web-page-config/sidebar");
        SidebarResult? result = await response.Content.ReadFromJsonAsync<SidebarResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result!.ShowSepa.ShouldBeTrue();
        result.ShowMail.ShouldBeTrue();
        result.Links[0].Link.ShouldBe(linkSettingsEntity.Link);
        result.Links[0].Name.ShouldBe(linkSettingsEntity.Name);
        result.Links[0].Icon.ShouldBe(linkSettingsEntity.Icon);
    }

    // ---------------------------------------------------------------
    // Helper functions
    // ---------------------------------------------------------------

    private WebPageConfigRequest CreateWebPageConfigRequest(string? name = null)
    {
        return new()
        {
            PageName = name ?? Guid.NewGuid().ToString(),
            Logo =
                "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=="
        };
    }

    private async Task<WebPageConfigEntity> CreateWebPageConfigEntity(bool isDeleted = false)
    {
        WebPageConfigEntity pageConfigEntity = new()
        {
            PageName = Guid.NewGuid().ToString(),
            Logo =
                "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==",
            DeletedAt = isDeleted ? DateTimeOffset.UtcNow : null
        };

        await WithDbContext(async db =>
        {
            await db.WebPageConfigs.AddAsync(pageConfigEntity);
            await db.SaveChangesAsync();
        });

        return pageConfigEntity;
    }

    private async Task<CreditorConfigEntity> CreateCreditorConfigEntity()
    {
        CryptoService cs = GetService<CryptoService>();
        CreditorConfigEntity creditorConfigEntity = new()
        {
            Name = Guid.NewGuid().ToString(),
            Iban_Encrypted = cs.Encrypt(Guid.NewGuid().ToString()),
            Bic_Encrypted = cs.Encrypt(Guid.NewGuid().ToString()),
            CreditorId = Guid.NewGuid().ToString(),
            StreetNameAndNumber = Guid.NewGuid().ToString(),
            PostCode = Guid.NewGuid().ToString(),
            CityName = Guid.NewGuid().ToString(),
            CountryCode = Guid.NewGuid().ToString()
        };

        await WithDbContext(async db =>
        {
            await db.CreditorConfigs.AddAsync(creditorConfigEntity);
            await db.SaveChangesAsync();
        });

        return creditorConfigEntity;
    }

    private async Task<MailSettingsEntity> CreateMailSettingsEntity()
    {
        MailSettingsEntity mailSettingsEntity = new()
        {
            SmtpServer = Guid.NewGuid().ToString(),
            Port = 8080,
            Username = Guid.NewGuid().ToString(),
            Password = Guid.NewGuid().ToString(),
            FromMail = Guid.NewGuid().ToString(),
            EnableSsl = true
        };

        await WithDbContext(async db =>
        {
            await db.MailSettings.AddAsync(mailSettingsEntity);
            await db.SaveChangesAsync();
        });

        return mailSettingsEntity;
    }

    private async Task<LinkSettingsEntity> CreateDataLinkSettingsEntity()
    {
        LinkSettingsEntity linkSettingsEntity = new()
        {
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