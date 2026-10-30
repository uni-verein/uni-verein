using System.Net;
using System.Net.Http.Json;
using UniVerein.Api.ApiRequests;
using UniVerein.DAL.Entities.Enums;
using UniVerein.IntegrationTests.Infrastructure;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class SessionRevocationTests : IntegrationTestBase
{
    public SessionRevocationTests(UniVereinWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public async Task PasswordChangeByAdmin_RevokesTheSessionsOfThatUser()
    {
        // Arrange
        (HttpClient user, Guid userId) = await CreateUserAndClientAsync(UserRole.USER, "session-user");
        (await user.GetAsync("/auth/me")).StatusCode.ShouldBe(HttpStatusCode.OK);

        // Act
        HttpResponseMessage response = await CreateAdminClient().PatchAsJsonAsync($"/users/{userId}",
            new UserUpdateRequest { Password = "Changed1234!" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.Contains("Set-Cookie").ShouldBeFalse();
        (await user.GetAsync("/auth/me")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PasswordChangeOfOwnAccount_RevokesOtherSessionsAndKeepsTheCurrentOne()
    {
        // Arrange
        (HttpClient admin, Guid adminId) = await CreateUserAndClientAsync(UserRole.ADMIN, "session-admin");
        string oldToken = admin.DefaultRequestHeaders.Authorization!.Parameter!;

        // Act
        HttpResponseMessage response = await admin.PatchAsJsonAsync($"/users/{adminId}",
            new UserUpdateRequest { Password = "Changed1234!", CurrentPassword = "Test1234!" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await GetSessionStatusAsync(oldToken)).ShouldBe(HttpStatusCode.Unauthorized);
        (await GetSessionStatusAsync(GetSessionToken(response))).ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task PasswordChangeViaAccountEndpoint_RevokesOtherSessionsAndKeepsTheCurrentOne()
    {
        // Arrange
        (HttpClient user, _) = await CreateUserAndClientAsync(UserRole.USER, "session-user");
        string oldToken = user.DefaultRequestHeaders.Authorization!.Parameter!;

        // Act
        HttpResponseMessage response = await user.PatchAsJsonAsync("/users/account",
            new UserUpdateRequest { Password = "Changed1234!", CurrentPassword = "Test1234!" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await GetSessionStatusAsync(oldToken)).ShouldBe(HttpStatusCode.Unauthorized);
        (await GetSessionStatusAsync(GetSessionToken(response))).ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateWithoutPasswordChange_KeepsTheSessionsOfThatUser()
    {
        // Arrange
        (HttpClient user, Guid userId) = await CreateUserAndClientAsync(UserRole.USER, "session-user");

        // Act
        HttpResponseMessage response = await CreateAdminClient().PatchAsJsonAsync($"/users/{userId}",
            new UserUpdateRequest { Email = "member@example.org" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await user.GetAsync("/auth/me")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Logout_RevokesTheSessionsOfTheUserOnEveryDevice()
    {
        // Arrange
        (HttpClient user, _) = await CreateUserAndClientAsync(UserRole.USER, "session-user");
        string token = user.DefaultRequestHeaders.Authorization!.Parameter!;
        HttpClient otherDevice = CreateClient().WithBearerToken(token);

        // Act
        HttpResponseMessage response = await user.PostAsync("/auth/logout", null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await otherDevice.GetAsync("/auth/me")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_WithCookieAndCsrfHeader_RevokesTheCopiedToken()
    {
        // Arrange
        HttpResponseMessage login = await CreateClient().PostAsJsonAsync("/auth/login",
            new LoginRequest { Username = "admin", Password = "Test1234!" });
        string token = GetSessionToken(login);
        HttpRequestMessage logout = new(HttpMethod.Post, "/auth/logout");
        logout.Headers.Add("Cookie", $"uv_auth={token}");
        logout.Headers.Add("X-UV-CSRF", "1");

        // Act
        HttpResponseMessage response = await CreateClient().SendAsync(logout);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetSessionStatusAsync(token)).ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_WithCookieButWithoutCsrfHeader_DoesNotRevokeTheSessions()
    {
        // Arrange
        HttpResponseMessage login = await CreateClient().PostAsJsonAsync("/auth/login",
            new LoginRequest { Username = "admin", Password = "Test1234!" });
        string token = GetSessionToken(login);
        HttpRequestMessage logout = new(HttpMethod.Post, "/auth/logout");
        logout.Headers.Add("Cookie", $"uv_auth={token}");

        // Act
        HttpResponseMessage response = await CreateClient().SendAsync(logout);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetSessionStatusAsync(token)).ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(null)]
    public async Task TokenWithStaleOrMissingSessionVersion_IsRejected(int? sessionVersion)
    {
        // Arrange
        (_, Guid userId) = await CreateUserAndClientAsync(UserRole.USER, "session-user");
        string token = JwtTestHelper.CreateToken(GetService<IConfiguration>(), userId, "session-user", UserRole.USER,
            sessionVersion: sessionVersion);

        // Act
        HttpStatusCode status = await GetSessionStatusAsync(token);

        // Assert
        status.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private static string GetSessionToken(HttpResponseMessage response)
    {
        string cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("uv_auth=", StringComparison.Ordinal));
        return cookie.Split(';')[0]["uv_auth=".Length..];
    }

    private async Task<HttpStatusCode> GetSessionStatusAsync(string token)
    {
        return (await CreateClient().WithBearerToken(token).GetAsync("/auth/me")).StatusCode;
    }
}
