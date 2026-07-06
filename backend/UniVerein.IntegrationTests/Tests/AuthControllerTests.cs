using System.Net;
using System.Net.Http.Json;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults;
using UniVerein.IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class AuthControllerTests : IntegrationTestBase
{
    public AuthControllerTests(UniVereinWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Theory]
    [InlineData("test", " ")]
    [InlineData(" ", "test")]
    public async Task Login_WithEmptyCredentials_ReturnsBadRequest(string username, string password)
    {
        // Arrange
        HttpClient client = CreateClient();
        LoginRequest request = new()
        {
            Username = username,
            Password = password
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/auth/login", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_LockUser_AfrerTreeFaildAttemps_ReturnsBadRequest()
    {
        // Arrange
        HttpClient client = CreateClient();
        LoginRequest request = new()
        {
            Username = "admin",
            Password = "Test1234"
        };

        // Act
        await client.PostAsJsonAsync("/auth/login", request);
        await client.PostAsJsonAsync("/auth/login", request);
        await client.PostAsJsonAsync("/auth/login", request);
        HttpResponseMessage response = await client.PostAsJsonAsync("/auth/login", request);
        LoginApiBlockedResult? result = await response.Content.ReadFromJsonAsync<LoginApiBlockedResult>();

        // Assert
        response.ShouldNotBeNull();
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        result.ShouldNotBeNull();
        result!.Error.ShouldBe("To many login attempts.");
        result!.RemainingTime.ShouldBeInRange(29, 30);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOkAndToken()
    {
        // Arrange
        HttpClient client = CreateClient();
        LoginRequest request = new()
        {
            Username = "admin",
            Password = "Test1234!"
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/auth/login", request);
        LoginApiResult? result = await response.Content.ReadFromJsonAsync<LoginApiResult>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result!.Token.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();
        LoginRequest request = new()
        {
            Username = "admin",
            Password = Guid.NewGuid().ToString()
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/auth/login", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithUnknownUser_ReturnsUnauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();
        LoginRequest request = new()
        {
            Username = "test",
            Password = Guid.NewGuid().ToString()
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/auth/login", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}