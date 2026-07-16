using System.Net;
using System.Net.Http.Json;
using UniVerein.Api.ApiResults;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using UniVerein.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class NotificationControllerTests : IntegrationTestBase
{
    public NotificationControllerTests(UniVereinWebApplicationFactory factory) : base(factory)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await WithDbContext(async db =>
        {
            db.FirmwareVersions.RemoveRange(db.FirmwareVersions.AsQueryable());
            await db.ForceSaveChangesAsync();
        });
    }

    public override async Task DisposeAsync()
    {
        await WithDbContext(async db =>
        {
            db.FirmwareVersions.RemoveRange(db.FirmwareVersions.AsQueryable());
            await db.ForceSaveChangesAsync();
        });
        await base.DisposeAsync();
    }

    // ---------------------------------------------------------------
    // GET /notifications/firmware-update
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetFirmwareUpdate_WithoutToken_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/notifications/firmware-update");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task GetFirmwareUpdate_Forbidden(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response = await client.GetAsync("/notifications/firmware-update");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetFirmwareUpdate_NoFirmwareInDb_NoContent()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.GetAsync("/notifications/firmware-update");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task GetFirmwareUpdate_TagNameDiffersFromCurrentVersion_UpdateAvailable()
    {
        // Arrange
        HttpClient client = CreateClientWithVersion("1.0.0");
        await CreateFirmwareVersionEntity(tagName: "1.1.0", version: "1.1.0");

        // Act
        HttpResponseMessage response = await client.GetAsync("/notifications/firmware-update");
        FirmwareUpdateResult? result = await response.Content.ReadFromJsonAsync<FirmwareUpdateResult>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result!.NewFirmwareAvailable.ShouldBeTrue();
        result.CurrentVersion.ShouldBe("1.0.0");
        result.LatestVersion.ShouldBe("1.1.0");
    }

    [Fact]
    public async Task GetFirmwareUpdate_TagNameMatchesCurrentVersion_NoUpdateAvailable()
    {
        // Arrange
        HttpClient client = CreateClientWithVersion("1.1.0");
        await CreateFirmwareVersionEntity(tagName: "1.1.0", version: "1.1.0");

        // Act
        HttpResponseMessage response = await client.GetAsync("/notifications/firmware-update");
        FirmwareUpdateResult? result = await response.Content.ReadFromJsonAsync<FirmwareUpdateResult>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result!.NewFirmwareAvailable.ShouldBeFalse();
        result.CurrentVersion.ShouldBe("1.1.0");
        result.LatestVersion.ShouldBe("1.1.0");
    }

    [Fact]
    public async Task GetFirmwareUpdate_MultipleFirmwareVersions_ReturnsMostRecent()
    {
        // Arrange
        HttpClient client = CreateClientWithVersion("1.0.0");
        Factory.FakeTime.SetUtcNow(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        await CreateFirmwareVersionEntity(tagName: "1.1.0", version: "1.1.0");
        Factory.FakeTime.SetUtcNow(new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));
        await CreateFirmwareVersionEntity(tagName: "1.2.0", version: "1.2.0");

        // Act
        HttpResponseMessage response = await client.GetAsync("/notifications/firmware-update");
        FirmwareUpdateResult? result = await response.Content.ReadFromJsonAsync<FirmwareUpdateResult>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result!.LatestVersion.ShouldBe("1.2.0");
    }

    // ---------------------------------------------------------------
    // Helper functions
    // ---------------------------------------------------------------

    private HttpClient CreateClientWithVersion(string? version)
    {
        WebApplicationFactory<UniVerein.Api.Startup> versionedFactory = Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Version"] = version
                });
            });
        });

        HttpClient client = versionedFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });

        IConfiguration configuration = versionedFactory.Services.GetRequiredService<IConfiguration>();
        return client.AsAdmin(configuration, UserId);
    }

    private async Task<FirmwareVersionEntity> CreateFirmwareVersionEntity(string tagName, string version)
    {
        FirmwareVersionEntity firmwareVersionEntity = new()
        {
            Id = Guid.NewGuid(),
            Version = version,
            TagName = tagName,
            PublishedAt = DateTimeOffset.UtcNow
        };

        await WithDbContext(async db =>
        {
            await db.FirmwareVersions.AddAsync(firmwareVersionEntity);
            await db.SaveChangesAsync();
        });

        return firmwareVersionEntity;
    }
}
