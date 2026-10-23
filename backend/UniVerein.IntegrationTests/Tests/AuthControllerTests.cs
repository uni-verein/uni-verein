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
    public async Task Login_FailedAttemptsOlderThanLockoutWindow_DoNotCountTowardsLockout()
    {
        // Arrange
        HttpClient client = CreateClient();
        LoginRequest request = new()
        {
            Username = "admin",
            Password = "Test1234"
        };

        DateTimeOffset originalTime = Factory.FakeTime.GetUtcNow();
        try
        {
            // Act
            await client.PostAsJsonAsync("/auth/login", request);
            await client.PostAsJsonAsync("/auth/login", request);

            Factory.FakeTime.SetUtcNow(originalTime.AddMinutes(16));
            HttpResponseMessage response = await client.PostAsJsonAsync("/auth/login", request);

            // Assert
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
        finally
        {
            Factory.FakeTime.SetUtcNow(originalTime);
        }
    }

    [Fact]
    public async Task Login_FailedAttemptsFromOneClient_DoNotBlockTheRealUserFromAnotherClient()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        for (int i = 0; i < 8; i++)
            await LoginAsync(client, "admin", "wrong-" + i, "203.0.113.50");

        HttpResponseMessage attackerResponse = await LoginAsync(client, "admin", "Test1234!", "203.0.113.50");
        HttpResponseMessage realUserResponse = await LoginAsync(client, "admin", "Test1234!", "198.51.100.7");

        // Assert
        attackerResponse.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        realUserResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_LockedClient_IsAlsoBlockedWithCorrectPassword()
    {
        // Arrange
        HttpClient client = CreateClient();
        for (int i = 0; i < 3; i++)
            await LoginAsync(client, "admin", "wrong-" + i, "203.0.113.50");

        // Act
        HttpResponseMessage response = await LoginAsync(client, "admin", "Test1234!", "203.0.113.50");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Login_ForwardedForHeaderOfClient_DoesNotEvadeLockout()
    {
        // Arrange
        HttpClient client = CreateClient();
        for (int i = 0; i < 3; i++)
            await LoginAsync(client, "admin", "wrong-" + i, forwardedFor: "198.51.100." + i);

        // Act
        HttpResponseMessage response = await LoginAsync(client, "admin", "wrong", forwardedFor: "198.51.100.99");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Login_UnknownUserName_IsLockedLikeAKnownUserName()
    {
        // Arrange
        HttpClient client = CreateClient();
        for (int i = 0; i < 3; i++)
            await LoginAsync(client, "does-not-exist", "wrong-" + i, "203.0.113.50");

        // Act
        HttpResponseMessage response = await LoginAsync(client, "does-not-exist", "wrong", "203.0.113.50");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Login_SuccessfulLogin_ResetsFailedAttemptsOfThatClient()
    {
        // Arrange
        HttpClient client = CreateClient();
        await LoginAsync(client, "admin", "wrong-1", "203.0.113.50");
        await LoginAsync(client, "admin", "wrong-2", "203.0.113.50");
        await LoginAsync(client, "admin", "Test1234!", "203.0.113.50");

        // Act
        await LoginAsync(client, "admin", "wrong-3", "203.0.113.50");
        await LoginAsync(client, "admin", "wrong-4", "203.0.113.50");
        HttpResponseMessage response = await LoginAsync(client, "admin", "Test1234!", "203.0.113.50");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private static Task<HttpResponseMessage> LoginAsync(HttpClient client, string username, string password,
        string? clientIp = null, string? forwardedFor = null)
    {
        HttpRequestMessage message = new(HttpMethod.Post, "/auth/login")
        {
            Content = JsonContent.Create(new LoginRequest { Username = username, Password = password })
        };
        if (clientIp != null)
            message.Headers.Add("X-Real-IP", clientIp);
        if (forwardedFor != null)
            message.Headers.Add("X-Forwarded-For", forwardedFor);

        return client.SendAsync(message);
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