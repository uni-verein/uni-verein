using System.Net;
using System.Net.Http.Json;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Services;
using UniVerein.DAL.Entities;
using UniVerein.IntegrationTests.Infrastructure;
using Shouldly;
using UniVerein.DAL.Entities.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class CreditorConfigControllerTests : IntegrationTestBase
{
    private readonly CryptoService _cryptoService;

    public CreditorConfigControllerTests(UniVereinWebApplicationFactory factory) : base(factory)
    {
        _cryptoService = GetService<CryptoService>();
    }

    public override async Task InitializeAsync()
    {
        await WithDbContext(async db =>
        {
            db.CreditorConfigs.RemoveRange(db.CreditorConfigs.AsQueryable());
            await db.ForceSaveChangesAsync();
        });
    }

    public override async Task DisposeAsync()
    {
        await WithDbContext(async db =>
        {
            db.CreditorConfigs.RemoveRange(db.CreditorConfigs.AsQueryable());
            await db.ForceSaveChangesAsync();
        });
    }

    [Fact]
    public async Task GetCreditorConfig_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/creditor-config");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task GetCreditorConfig_WithExpiredToken_Unauthorized(UserRole role)
    {
        // Arrange
        IConfiguration configuration = Factory.Services.GetRequiredService<IConfiguration>();
        var expiredToken = JwtTestHelper.CreateToken(
            configuration,
            userId: Guid.NewGuid(),
            username: "expired",
            role: role,
            lifetime: TimeSpan.FromMinutes(-5));

        var client = CreateClient().WithBearerToken(expiredToken);

        // Act
        var response = await client.GetAsync("/creditor-config");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task GetCreditorConfig_Forbidden(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response = await client.GetAsync("/creditor-config");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetCreditorConfig_NotFound()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.GetAsync("/creditor-config");
        ErrorDetailsResult? result = await response.Content.ReadFromJsonAsync<ErrorDetailsResult>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.ShouldNotBeNull();
        result!.StatusCode.ShouldBe((int)HttpStatusCode.NotFound);
        result.ErrorMessage.ShouldBe("Creditor config not found.");
    }

    [Fact]
    public async Task GetCreditorConfig_Authorized()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        await CreateCreditorConfigEntity();

        // Act
        HttpResponseMessage response = await client.GetAsync("/creditor-config");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetCreditorConfig_Success()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        CreditorConfigEntity creditorConfig = await CreateCreditorConfigEntity();

        // Act
        HttpResponseMessage response = await client.GetAsync("/creditor-config");
        CreditorConfigResult? result = await response.Content.ReadFromJsonAsync<CreditorConfigResult>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.ShouldNotBeNull();
        CompareCreditorConfig(creditorConfig, result!);
    }

    // ---------------------------------------------------------------
    // CREATE/UPDATE /api/creditor-config
    // ---------------------------------------------------------------

    [Fact]
    public async Task CreateCreditorConfig_WithoutToken_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync("/creditor-config", CreateCreditorConfigRequest());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task CreateCreditorConfig_Forbidden(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync("/creditor-config", CreateCreditorConfigRequest());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateCreditorConfig_Success()
    {
        // Arrange
        var client = CreateClient(UserRole.ADMIN);
        CreditorConfigRequest request = CreateCreditorConfigRequest();

        // Act
        var response = await client.PutAsJsonAsync("/creditor-config", request);
        CreditorConfigResult? result = await response.Content.ReadFromJsonAsync<CreditorConfigResult>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result!.Name.ShouldBe(request.Name);
        await WithDbContext(async db =>
        {
            CreditorConfigEntity? creditorConfig =
                await db.CreditorConfigs.FirstOrDefaultAsync(c => c.Name == result.Name);
            creditorConfig.ShouldNotBeNull();
            CompareCreditorConfig(creditorConfig!, result);
        });
    }

    [Fact]
    public async Task UpdateCreditorConfig_Success()
    {
        // Arrange
        var client = CreateClient(UserRole.ADMIN);
        await CreateCreditorConfigEntity();
        CreditorConfigRequest request = CreateCreditorConfigRequest();

        // Act
        var response = await client.PutAsJsonAsync("/creditor-config", request);
        CreditorConfigResult? result = await response.Content.ReadFromJsonAsync<CreditorConfigResult>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result!.Name.ShouldBe(request.Name);
        await WithDbContext(async db =>
        {
            CreditorConfigEntity? creditorConfig =
                await db.CreditorConfigs.FirstOrDefaultAsync(c => c.Name == result.Name);
            creditorConfig.ShouldNotBeNull();
            CompareCreditorConfig(creditorConfig!, result);
        });
    }

    // ---------------------------------------------------------------
    // DELETE /api/members/{id}
    // ---------------------------------------------------------------

    [Fact]
    public async Task DeleteCreditorConfig_WithoutToken_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/creditor-config/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task DeleteCreditorConfig_Forbidden(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/creditor-config/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteCreditorConfig_NotFound()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/creditor-config/{Guid.NewGuid()}");
        ErrorDetailsResult? result = await response.Content.ReadFromJsonAsync<ErrorDetailsResult>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.ShouldNotBeNull();
        result!.StatusCode.ShouldBe((int)HttpStatusCode.NotFound);
        result.ErrorMessage.ShouldBe("Creditor config not found.");
    }

    [Fact]
    public async Task DeleteCreditorConfig_AsAdmin_ReturnsNoOk()
    {
        HttpClient client = CreateAdminClient();
        CreditorConfigEntity creditorConfigEntity = await CreateCreditorConfigEntity();

        // Act
        var response = await client.DeleteAsync($"/creditor-config/{creditorConfigEntity.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ---------------------------------------------------------------
    // Helper functions
    // ---------------------------------------------------------------

    private async Task<CreditorConfigEntity> CreateCreditorConfigEntity(Guid? id = null, string? username = null)
    {
        CreditorConfigEntity creditorConfigEntity = new()
        {
            Id = id ?? Guid.NewGuid(),
            Name = username ?? Guid.NewGuid().ToString(),
            Iban_Encrypted = _cryptoService.Encrypt(Guid.NewGuid().ToString()),
            Bic_Encrypted = _cryptoService.Encrypt(Guid.NewGuid().ToString()),
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

    private void CompareCreditorConfig(CreditorConfigEntity entity, CreditorConfigResult result)
    {
        entity.Id.ShouldBe(result.Id);
        entity.Name.ShouldBe(result.Name);
        _cryptoService.Decrypt(entity.Iban_Encrypted).ShouldBe(result.Iban);
        _cryptoService.Decrypt(entity.Bic_Encrypted).ShouldBe(result.Bic);
        entity.CreditorId.ShouldBe(result.CreditorId);
        entity.StreetNameAndNumber.ShouldBe(result.StreetNameAndNumber);
        entity.PostCode.ShouldBe(result.PostCode);
        entity.CityName.ShouldBe(result.CityName);
        entity.CountryCode.ShouldBe(result.CountryCode);
    }

    private CreditorConfigRequest CreateCreditorConfigRequest(string? name = null, string? streetNameAndNumber = null)
    {
        return new CreditorConfigRequest()
        {
            Name = name ?? Guid.NewGuid().ToString(),
            Iban = Guid.NewGuid().ToString(),
            Bic = Guid.NewGuid().ToString(),
            CreditorId = Guid.NewGuid().ToString(),
            StreetNameAndNumber = streetNameAndNumber ?? Guid.NewGuid().ToString(),
            PostCode = Guid.NewGuid().ToString(),
            CityName = Guid.NewGuid().ToString(),
            CountryCode = Guid.NewGuid().ToString(),
        };
    }
}