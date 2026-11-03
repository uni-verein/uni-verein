using System.Net;
using System.Net.Http.Json;
using UniVerein.Api;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Services;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using UniVerein.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class SetupControllerTests : IntegrationTestBase
{
    private const string SetupToken = "test-setup-token";
    private const string ValidPassword = "ValidPassword1";
    private const string ValidEmail = "owner@example.org";

    public SetupControllerTests(UniVereinWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task Status_WithExistingUser_ReturnsNoSetupNeeded()
    {
        // Arrange
        HttpClient client = CreateSetupClient(SetupToken);

        // Act
        HttpResponseMessage response = await client.GetAsync("/setup/status");
        SetupStatusResult? result = await response.Content.ReadFromJsonAsync<SetupStatusResult>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.NeedsSetup.ShouldBeFalse();
    }

    [Fact]
    public async Task Status_WithoutUsers_ReturnsSetupNeeded()
    {
        // Arrange
        await RemoveAllUsersAsync();
        HttpClient client = CreateSetupClient(SetupToken);

        // Act
        HttpResponseMessage response = await client.GetAsync("/setup/status");
        SetupStatusResult? result = await response.Content.ReadFromJsonAsync<SetupStatusResult>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.NeedsSetup.ShouldBeTrue();
    }

    [Fact]
    public async Task Status_WithOnlySoftDeletedUsers_ReturnsNoSetupNeeded()
    {
        // Arrange
        await WithDbContext(async db =>
        {
            foreach (UserEntity user in db.Users.IgnoreQueryFilters())
                user.DeletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        });
        HttpClient client = CreateSetupClient(SetupToken);

        // Act
        SetupStatusResult? result = await client.GetFromJsonAsync<SetupStatusResult>("/setup/status");

        // Assert
        result.ShouldNotBeNull();
        result.NeedsSetup.ShouldBeFalse();
    }

    [Fact]
    public async Task Status_CalledOften_DoesNotConsumeTheLoginRateLimit()
    {
        // Arrange
        HttpClient client = CreateSetupClient(SetupToken,
            new Dictionary<string, string?> { ["RateLimiting:AuthLogin:PermitLimit"] = "3" });

        // Act
        List<HttpStatusCode> statusCodes = [];
        for (int i = 0; i < 10; i++)
            statusCodes.Add((await client.GetAsync("/setup/status")).StatusCode);
        HttpResponseMessage login = await client.PostAsJsonAsync("/auth/login",
            new LoginRequest { Username = "admin", Password = "wrong-password" });

        // Assert
        statusCodes.ShouldAllBe(code => code == HttpStatusCode.OK);
        login.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Setup_WithValidToken_CreatesAdminWhoCanLogIn()
    {
        // Arrange
        await RemoveAllUsersAsync();
        HttpClient client = CreateSetupClient(SetupToken);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/setup", CreateRequest(SetupToken));
        HttpResponseMessage login = await client.PostAsJsonAsync("/auth/login",
            new LoginRequest { Username = "Owner", Password = ValidPassword });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        login.StatusCode.ShouldBe(HttpStatusCode.OK);
        await WithDbContext(db =>
        {
            UserEntity user = db.Users.Single();
            user.Username.ShouldBe("Owner");
            user.Role.ShouldBe(UserRole.ADMIN);
            GetService<CryptoService>().Decrypt(user.Email).ShouldBe(ValidEmail);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Setup_WithWrongToken_ReturnsForbiddenAndCreatesNoUser()
    {
        // Arrange
        await RemoveAllUsersAsync();
        HttpClient client = CreateSetupClient(SetupToken);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/setup", CreateRequest("wrong-token"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await WithDbContext(db =>
        {
            db.Users.IgnoreQueryFilters().Any().ShouldBeFalse();
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Setup_WithoutConfiguredToken_ReturnsForbiddenAndCreatesNoUser()
    {
        // Arrange
        await RemoveAllUsersAsync();
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/setup", CreateRequest(SetupToken));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await WithDbContext(db =>
        {
            db.Users.IgnoreQueryFilters().Any().ShouldBeFalse();
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Setup_WhenUsersExist_ReturnsConflict()
    {
        // Arrange
        HttpClient client = CreateSetupClient(SetupToken);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/setup", CreateRequest(SetupToken));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Setup_CalledTwice_SecondCallReturnsConflict()
    {
        // Arrange
        await RemoveAllUsersAsync();
        HttpClient client = CreateSetupClient(SetupToken);

        // Act
        HttpResponseMessage first = await client.PostAsJsonAsync("/setup", CreateRequest(SetupToken));
        HttpResponseMessage second = await client.PostAsJsonAsync("/setup", CreateRequest(SetupToken, "Other"));

        // Assert
        first.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await WithDbContext(db =>
        {
            db.Users.Count().ShouldBe(1);
            return Task.CompletedTask;
        });
    }

    [Theory]
    [InlineData("Owner", ValidEmail, "short")]
    [InlineData("Owner", ValidEmail, "ThisPasswordIsMuchTooLongBecauseItExceedsFiftyCharacters1")]
    [InlineData("ThisUsernameIsMuchTooLongBecauseItExceedsFiftyCharacters", ValidEmail, ValidPassword)]
    [InlineData(" ", ValidEmail, ValidPassword)]
    [InlineData("Owner", "not-an-email", ValidPassword)]
    [InlineData("Owner", "averyveryverylongaddressthatexceedsfiftycharacters@example.org", ValidPassword)]
    public async Task Setup_WithInvalidCredentials_ReturnsBadRequest(string username, string email, string password)
    {
        // Arrange
        await RemoveAllUsersAsync();
        HttpClient client = CreateSetupClient(SetupToken);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/setup",
            new SetupRequest { Token = SetupToken, Username = username, Email = email, Password = password });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await WithDbContext(db =>
        {
            db.Users.IgnoreQueryFilters().Any().ShouldBeFalse();
            return Task.CompletedTask;
        });
    }

    private static SetupRequest CreateRequest(string token, string username = "Owner")
    {
        return new SetupRequest { Token = token, Username = username, Email = ValidEmail, Password = ValidPassword };
    }

    private async Task RemoveAllUsersAsync()
    {
        await WithDbContext(async db =>
        {
            db.Users.RemoveRange(db.Users.IgnoreQueryFilters());
            await db.ForceSaveChangesAsync();
        });
    }

    private HttpClient CreateSetupClient(string token, Dictionary<string, string?>? overrides = null)
    {
        Dictionary<string, string?> settings = new(overrides ?? []) { ["Setup:Token"] = token };
        WebApplicationFactory<Startup> setupFactory = Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(settings);
            });
        });

        return setupFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });
    }
}
