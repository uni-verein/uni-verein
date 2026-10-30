using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Security;
using UniVerein.Api.Services;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using UniVerein.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class TwoFactorTests : IntegrationTestBase
{
    private const string Username = "tfa-user";
    private const string Password = "Test1234!";

    public TwoFactorTests(UniVereinWebApplicationFactory factory)
        : base(factory)
    {
    }

    [Fact]
    public void TryVerifyCode_WithRfc6238TestVector_Accepts()
    {
        // Arrange
        DateTimeOffset originalTime = Factory.FakeTime.GetUtcNow();
        try
        {
            Factory.FakeTime.SetUtcNow(DateTimeOffset.FromUnixTimeSeconds(59));
            TwoFactorService service = GetService<TwoFactorService>();

            // Act
            bool valid = service.TryVerifyCode("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ", "287082", null, out long step);

            // Assert
            valid.ShouldBeTrue();
            step.ShouldBe(1);
        }
        finally
        {
            Factory.FakeTime.SetUtcNow(originalTime);
        }
    }

    [Fact]
    public async Task Login_WithoutTwoFactor_StillReturnsToken()
    {
        // Arrange
        await CreateUserAndClientAsync(UserRole.USER, Username);

        // Act
        HttpResponseMessage response = await PasswordLoginAsync();
        LoginApiResult? result = await response.Content.ReadFromJsonAsync<LoginApiResult>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result!.Token.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_WithTwoFactorEnabled_IssuesNoSessionBeforeSecondFactor()
    {
        // Arrange
        await EnableTwoFactorAsync();

        // Act
        HttpResponseMessage response = await PasswordLoginAsync();
        LoginTwoFactorRequiredApiResult? result = await response.Content.ReadFromJsonAsync<LoginTwoFactorRequiredApiResult>();
        string body = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result!.RequiresTwoFactor.ShouldBeTrue();
        result.Challenge.ShouldNotBeNullOrWhiteSpace();
        body.ShouldNotContain("\"token\"");
        response.Headers.Contains("Set-Cookie").ShouldBeFalse();
    }

    [Fact]
    public async Task LoginTwoFactor_WithValidCode_SetsSessionAndReturnsToken()
    {
        // Arrange
        (string secret, _) = await EnableTwoFactorAsync();
        string challenge = await GetChallengeAsync();

        // Act
        HttpResponseMessage response = await WithTimeOffsetAsync(30, () => TwoFactorLoginAsync(challenge, CodeAt(secret, 30)));
        LoginApiResult? result = await response.Content.ReadFromJsonAsync<LoginApiResult>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result!.Token.ShouldNotBeNullOrWhiteSpace();
        response.Headers.GetValues("Set-Cookie").ShouldContain(c => c.StartsWith("uv_auth=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoginTwoFactor_ReusingAnAcceptedCode_IsRejected()
    {
        // Arrange
        (string secret, _) = await EnableTwoFactorAsync();
        string challenge = await GetChallengeAsync();
        string code = CodeAt(secret, 30);

        // Act
        HttpResponseMessage first = await WithTimeOffsetAsync(30, () => TwoFactorLoginAsync(challenge, code));
        HttpResponseMessage replay = await WithTimeOffsetAsync(30, () => TwoFactorLoginAsync(challenge, code));

        // Assert
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        replay.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LoginTwoFactor_WithWrongCode_ReturnsUnauthorizedThenLocksOut()
    {
        // Arrange
        await EnableTwoFactorAsync();
        string challenge = await GetChallengeAsync();

        // Act
        HttpResponseMessage first = await TwoFactorLoginAsync(challenge, "000000");
        await TwoFactorLoginAsync(challenge, "000001");
        await TwoFactorLoginAsync(challenge, "000002");
        HttpResponseMessage blocked = await TwoFactorLoginAsync(challenge, "000003");

        // Assert
        first.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        blocked.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task LoginTwoFactor_WithCodeTwoStepsInTheFuture_ReturnsUnauthorized()
    {
        // Arrange
        (string secret, _) = await EnableTwoFactorAsync();
        string challenge = await GetChallengeAsync();

        // Act
        HttpResponseMessage response = await TwoFactorLoginAsync(challenge, CodeAt(secret, 90));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Setup_ReturnsOtpAuthUriWithIssuerAndTotpParameters()
    {
        // Arrange
        (HttpClient client, _) = await CreateUserAndClientAsync(UserRole.USER, Username);

        // Act
        HttpResponseMessage response = await SetupAsync(client);
        TwoFactorSetupApiResult result = (await response.Content.ReadFromJsonAsync<TwoFactorSetupApiResult>())!;
        Uri uri = new(result.OtpAuthUri);
        System.Collections.Specialized.NameValueCollection query = System.Web.HttpUtility.ParseQueryString(uri.Query);

        // Assert
        uri.Scheme.ShouldBe("otpauth");
        uri.Host.ShouldBe("totp");
        Uri.UnescapeDataString(uri.AbsolutePath).ShouldBe($"/UniVerein:{Username}");
        query["secret"].ShouldBe(result.Secret);
        query["issuer"].ShouldBe("UniVerein");
        query["digits"].ShouldBe("6");
        query["period"].ShouldBe("30");
        result.Secret.ShouldMatch("^[A-Z2-7]{32}$");
    }

    [Fact]
    public async Task Setup_WithWrongPassword_ReturnsBadRequestAndCreatesNoSecret()
    {
        // Arrange
        (HttpClient client, Guid userId) = await CreateUserAndClientAsync(UserRole.USER, Username);

        // Act
        HttpResponseMessage response = await SetupAsync(client, "wrong-password");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await WithDbContext(async db => (await db.Users.AsNoTracking().FirstAsync(x => x.Id == userId)).TotpSecret.ShouldBeNull());
    }

    [Fact]
    public async Task Setup_WithoutPassword_ReturnsBadRequest()
    {
        // Arrange
        (HttpClient client, _) = await CreateUserAndClientAsync(UserRole.USER, Username);

        // Act
        HttpResponseMessage response = await client.PostAsync("/users/account/2fa/setup", null);
        HttpResponseMessage emptyBody = await client.PostAsJsonAsync("/users/account/2fa/setup", new { password = string.Empty });

        // Assert
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnsupportedMediaType);
        emptyBody.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("2fa-setup")]
    [InlineData("2fa-enable")]
    [InlineData("2fa-disable")]
    [InlineData("2fa")]
    public async Task Setup_AfterFailedLoginsWithAnInternalLookingUsername_IsNotLockedOut(string prefix)
    {
        // Arrange
        (HttpClient client, Guid userId) = await CreateUserAndClientAsync(UserRole.USER, Username);
        for (int i = 0; i < 4; i++)
            await CreateClient().PostAsJsonAsync("/auth/login", new LoginRequest { Username = $"{prefix}:{userId}", Password = "wrong-" + i });

        // Act
        HttpResponseMessage response = await SetupAsync(client);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Setup_WithRepeatedWrongPasswords_LocksOutEvenWithCorrectPassword()
    {
        // Arrange
        (HttpClient client, _) = await CreateUserAndClientAsync(UserRole.USER, Username);
        for (int i = 0; i < 3; i++)
            await SetupAsync(client, "wrong-" + i);

        // Act
        HttpResponseMessage response = await SetupAsync(client);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Enable_WithoutPendingSetup_ReturnsBadRequest()
    {
        // Arrange
        (HttpClient client, Guid userId) = await CreateUserAndClientAsync(UserRole.USER, Username);
        await SetupAsync(client, "wrong-password");

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/users/account/2fa/enable", new TwoFactorCodeRequest { Code = "123456" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await IsTwoFactorEnabledAsync(userId)).ShouldBeFalse();
    }

    [Fact]
    public async Task LoginTwoFactor_WrongCodesFromManyAddresses_BlockTheAccountEvenForTheValidCode()
    {
        // Arrange
        (string secret, _) = await EnableTwoFactorAsync();
        string challenge = await GetChallengeAsync();

        // Act
        for (int i = 0; i < AccountLockoutTracker.MaxFailures; i++)
            (await TwoFactorLoginFromAsync(challenge, "00000" + i, $"198.51.100.{i + 1}")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        HttpResponseMessage fromNewAddress = await WithTimeOffsetAsync(30, () => TwoFactorLoginFromAsync(challenge, CodeAt(secret, 30), "203.0.113.200"));
        LoginApiBlockedResult? blocked = await fromNewAddress.Content.ReadFromJsonAsync<LoginApiBlockedResult>();

        // Assert
        fromNewAddress.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        blocked!.RemainingTime.ShouldBeInRange(14 * 60, 15 * 60);
        blocked.RecoveryCodeAllowed.ShouldBeTrue();
    }

    [Fact]
    public async Task LoginTwoFactor_ParallelWrongCodesFromManyAddresses_AreLimitedToMaxFailures()
    {
        // Arrange
        await EnableTwoFactorAsync();
        string challenge = await GetChallengeAsync();

        // Act
        HttpResponseMessage[] responses = await Task.WhenAll(Enumerable.Range(0, 30)
            .Select(i => TwoFactorLoginFromAsync(challenge, (100000 + i).ToString(), $"198.51.100.{i + 1}")));

        // Assert
        responses.Count(r => r.StatusCode == HttpStatusCode.Unauthorized).ShouldBe(AccountLockoutTracker.MaxFailures);
        responses.Count(r => r.StatusCode == HttpStatusCode.Forbidden).ShouldBe(30 - AccountLockoutTracker.MaxFailures);
    }

    [Fact]
    public async Task LoginTwoFactor_ValidCodeAfterWrongCodes_ResetsTheReservedAttempts()
    {
        // Arrange
        (string secret, _) = await EnableTwoFactorAsync();
        string challenge = await GetChallengeAsync();
        for (int i = 0; i < AccountLockoutTracker.MaxFailures - 1; i++)
            (await TwoFactorLoginFromAsync(challenge, "00000" + i, $"198.51.100.{i + 1}")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Act
        HttpResponseMessage valid = await WithTimeOffsetAsync(30, () => TwoFactorLoginFromAsync(challenge, CodeAt(secret, 30), "203.0.113.200"));

        // Assert
        valid.StatusCode.ShouldBe(HttpStatusCode.OK);
        GetService<AccountLockoutTracker>().GetRemainingLockout(_userId).ShouldBeNull();
        (await TwoFactorLoginFromAsync(challenge, "000009", "203.0.113.201")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LoginTwoFactor_WhileTheAccountIsBlocked_AcceptsARecoveryCode()
    {
        // Arrange
        (_, List<string> recoveryCodes) = await EnableTwoFactorAsync();
        string challenge = await GetChallengeAsync();
        for (int i = 0; i < AccountLockoutTracker.MaxFailures; i++)
            await TwoFactorLoginFromAsync(challenge, "00000" + i, $"198.51.100.{i + 1}");

        // Act
        HttpResponseMessage response = await TwoFactorLoginFromAsync(challenge, recoveryCodes[0], "203.0.113.202");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LoginTwoFactor_WrongRecoveryCodes_DoNotBlockTheAccount()
    {
        // Arrange
        (string secret, _) = await EnableTwoFactorAsync();
        string challenge = await GetChallengeAsync();
        for (int i = 0; i < AccountLockoutTracker.MaxFailures + 2; i++)
            await TwoFactorLoginFromAsync(challenge, $"AAAA-BBBB-CCC{i}", $"198.51.100.{i + 1}");

        string code = CodeAt(secret, 30);

        // Act
        HttpResponseMessage response = await WithTimeOffsetAsync(30, () => TwoFactorLoginFromAsync(challenge, code, "203.0.113.203"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LoginTwoFactor_ValidDigitsHiddenBetweenLetters_IsNotAccepted()
    {
        // Arrange
        (string secret, _) = await EnableTwoFactorAsync();
        string challenge = await GetChallengeAsync();
        string disguised = string.Join("x", CodeAt(secret, 30).Select(c => c.ToString()));

        // Act
        HttpResponseMessage response = await WithTimeOffsetAsync(30, () => TwoFactorLoginFromAsync(challenge, disguised, "203.0.113.204"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LoginTwoFactor_CodeWithASpace_IsAccepted()
    {
        // Arrange
        (string secret, _) = await EnableTwoFactorAsync();
        string challenge = await GetChallengeAsync();

        string code = CodeAt(secret, 30);

        // Act
        HttpResponseMessage response = await WithTimeOffsetAsync(30, () => TwoFactorLoginFromAsync(challenge, $"{code[..3]} {code[3..]}", "203.0.113.205"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LoginTwoFactor_AfterTheAccountBlockExpired_AcceptsTheValidCodeAgain()
    {
        // Arrange
        (string secret, _) = await EnableTwoFactorAsync();
        string challenge = await GetChallengeAsync();
        for (int i = 0; i < AccountLockoutTracker.MaxFailures; i++)
            await TwoFactorLoginFromAsync(challenge, "00000" + i, $"198.51.100.{i + 1}");

        // Act
        HttpResponseMessage response = await WithTimeOffsetAsync(16 * 60, async () =>
        {
            string newChallenge = await GetChallengeAsync();
            return await TwoFactorLoginFromAsync(newChallenge, CodeAt(secret, 0), "203.0.113.201");
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LoginTwoFactor_WrongCodesOfOneAccount_DoNotBlockAnotherAccount()
    {
        // Arrange
        (string secret, _) = await EnableTwoFactorAsync();
        string challenge = await GetChallengeAsync();
        for (int i = 0; i < AccountLockoutTracker.MaxFailures; i++)
            await TwoFactorLoginFromAsync(challenge, "00000" + i, $"198.51.100.{i + 1}");

        // Act
        string otherChallenge = await EnableTwoFactorForOtherUserAsync();
        HttpResponseMessage response = await TwoFactorLoginFromAsync(otherChallenge, "999999", "198.51.100.5");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LoginTwoFactor_WithExpiredChallenge_ReturnsUnauthorized()
    {
        // Arrange
        (string secret, _) = await EnableTwoFactorAsync();
        string challenge = await GetChallengeAsync();

        // Act
        HttpResponseMessage response = await WithTimeOffsetAsync(360, () => TwoFactorLoginAsync(challenge, CodeAt(secret, 360)));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LoginTwoFactor_WithTamperedChallenge_ReturnsUnauthorized()
    {
        // Arrange
        (string secret, _) = await EnableTwoFactorAsync();
        string challenge = await GetChallengeAsync();
        string tampered = challenge[..^2] + (challenge.EndsWith("AA") ? "BB" : "AA");

        // Act
        HttpResponseMessage response = await WithTimeOffsetAsync(30, () => TwoFactorLoginAsync(tampered, CodeAt(secret, 30)));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LoginTwoFactor_WithRecoveryCode_WorksOnlyOnce()
    {
        // Arrange
        (_, List<string> recoveryCodes) = await EnableTwoFactorAsync();
        string challenge = await GetChallengeAsync();

        // Act
        HttpResponseMessage first = await TwoFactorLoginAsync(challenge, recoveryCodes[0]);
        HttpResponseMessage second = await TwoFactorLoginAsync(challenge, recoveryCodes[0]);

        // Assert
        recoveryCodes.Count.ShouldBe(TwoFactorService.RecoveryCodeCount);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        second.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Enable_CreatesRecoveryCodesOf12CharactersAndStoresOnlyHashes()
    {
        // Arrange + Act
        (_, List<string> recoveryCodes) = await EnableTwoFactorAsync();
        List<UserRecoveryCodeEntity> stored = await GetRecoveryCodesAsync();

        // Assert
        recoveryCodes.ShouldAllBe(c => System.Text.RegularExpressions.Regex.IsMatch(c, "^[A-Z2-7]{4}-[A-Z2-7]{4}-[A-Z2-7]{4}$"));
        recoveryCodes.Distinct().Count().ShouldBe(TwoFactorService.RecoveryCodeCount);
        stored.Count.ShouldBe(TwoFactorService.RecoveryCodeCount);
        stored.ShouldAllBe(c => c.UsedAt == null && c.UserId == _userId);
        stored.Select(c => c.CodeHash).ShouldNotContain(h => recoveryCodes.Any(code => h.Contains(code.Replace("-", string.Empty))));
    }

    [Fact]
    public async Task LoginTwoFactor_RecoveryCodeWithoutDashesInLowercase_IsAcceptedAndKeptAsUsed()
    {
        // Arrange
        (_, List<string> recoveryCodes) = await EnableTwoFactorAsync();
        string challenge = await GetChallengeAsync();

        // Act
        HttpResponseMessage response = await TwoFactorLoginAsync(challenge, recoveryCodes[3].Replace("-", string.Empty).ToLowerInvariant());
        List<UserRecoveryCodeEntity> stored = await GetRecoveryCodesAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        stored.Count.ShouldBe(TwoFactorService.RecoveryCodeCount);
        stored.Count(c => c.UsedAt != null).ShouldBe(1);
    }

    [Fact]
    public async Task TryConsumeRecoveryCode_CalledConcurrently_SucceedsExactlyOnce()
    {
        // Arrange
        (_, List<string> recoveryCodes) = await EnableTwoFactorAsync();

        // Act
        bool[] results = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            using IServiceScope scope = Factory.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<TwoFactorService>().TryConsumeRecoveryCodeAsync(_userId, recoveryCodes[0]);
        }));

        // Assert
        results.Count(r => r).ShouldBe(1);
    }

    [Fact]
    public async Task Enable_WithWrongCode_ReturnsBadRequestAndStaysDisabled()
    {
        // Arrange
        (HttpClient client, Guid userId) = await CreateUserAndClientAsync(UserRole.USER, Username);
        await SetupAsync(client);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/users/account/2fa/enable", new TwoFactorCodeRequest { Code = "000000" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await IsTwoFactorEnabledAsync(userId)).ShouldBeFalse();
    }

    [Fact]
    public async Task Disable_WithPasswordAndCode_TurnsTwoFactorOff()
    {
        // Arrange
        (string secret, _) = await EnableTwoFactorAsync();

        // Act
        HttpResponseMessage response = await WithTimeOffsetAsync(30, () => _client!.PostAsJsonAsync("/users/account/2fa/disable",
            new TwoFactorDisableRequest { Password = Password, Code = CodeAt(secret, 30) }));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await IsTwoFactorEnabledAsync(_userId)).ShouldBeFalse();
        (await GetRecoveryCodesAsync()).ShouldBeEmpty();
        (await PasswordLoginAsync()).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Disable_WithWrongPassword_ReturnsBadRequest()
    {
        // Arrange
        (string secret, _) = await EnableTwoFactorAsync();

        // Act
        HttpResponseMessage response = await WithTimeOffsetAsync(30, () => _client!.PostAsJsonAsync("/users/account/2fa/disable",
            new TwoFactorDisableRequest { Password = "wrong-password", Code = CodeAt(secret, 30) }));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await IsTwoFactorEnabledAsync(_userId)).ShouldBeTrue();
    }

    [Fact]
    public async Task Reset_AsAdmin_RemovesTwoFactorOfAnotherUser()
    {
        // Arrange
        await EnableTwoFactorAsync();
        HttpClient admin = CreateAdminClient();

        // Act
        HttpResponseMessage response = await admin.DeleteAsync($"/users/{_userId}/2fa");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await IsTwoFactorEnabledAsync(_userId)).ShouldBeFalse();
        (await GetRecoveryCodesAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task Reset_AsNormalUser_IsForbidden()
    {
        // Arrange
        await EnableTwoFactorAsync();
        (HttpClient other, _) = await CreateUserAndClientAsync(UserRole.USER, "other-user");

        // Act
        HttpResponseMessage response = await other.DeleteAsync($"/users/{_userId}/2fa");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await IsTwoFactorEnabledAsync(_userId)).ShouldBeTrue();
    }

    [Fact]
    public async Task Enable_RevokesExistingSessionsAndKeepsTheCurrentOne()
    {
        // Arrange
        (HttpClient client, Guid userId) = await CreateUserAndClientAsync(UserRole.USER, Username);
        string oldToken = client.DefaultRequestHeaders.Authorization!.Parameter!;
        TwoFactorSetupApiResult setup = (await (await SetupAsync(client)).Content.ReadFromJsonAsync<TwoFactorSetupApiResult>())!;

        // Act
        HttpResponseMessage enable = await client.PostAsJsonAsync("/users/account/2fa/enable",
            new TwoFactorCodeRequest { Code = CodeAt(setup.Secret, 0) });

        // Assert
        enable.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await GetSessionStatusAsync(oldToken)).ShouldBe(HttpStatusCode.Unauthorized);
        (await GetSessionStatusAsync(GetSessionToken(enable))).ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Disable_RevokesExistingSessionsAndKeepsTheCurrentOne()
    {
        // Arrange
        (string secret, _) = await EnableTwoFactorAsync();
        string oldToken = _client!.DefaultRequestHeaders.Authorization!.Parameter!;

        // Act
        HttpResponseMessage response = await WithTimeOffsetAsync(30, () => _client!.PostAsJsonAsync("/users/account/2fa/disable",
            new TwoFactorDisableRequest { Password = Password, Code = CodeAt(secret, 30) }));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await GetSessionStatusAsync(oldToken)).ShouldBe(HttpStatusCode.Unauthorized);
        (await GetSessionStatusAsync(GetSessionToken(response))).ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Reset_AsAdmin_RevokesTheSessionsOfTheUser()
    {
        // Arrange
        await EnableTwoFactorAsync();
        string userToken = _client!.DefaultRequestHeaders.Authorization!.Parameter!;

        // Act
        HttpResponseMessage response = await CreateAdminClient().DeleteAsync($"/users/{_userId}/2fa");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        response.Headers.Contains("Set-Cookie").ShouldBeFalse();
        (await GetSessionStatusAsync(userToken)).ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LoginTwoFactor_ChallengeIssuedBeforeAPasswordChange_IsRejected()
    {
        // Arrange
        (string secret, _) = await EnableTwoFactorAsync();
        string challenge = await GetChallengeAsync();
        HttpResponseMessage change = await CreateAdminClient().PatchAsJsonAsync($"/users/{_userId}",
            new UserUpdateRequest { Password = "Changed1234!" });
        change.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Act
        HttpResponseMessage response = await WithTimeOffsetAsync(30, () => TwoFactorLoginAsync(challenge, CodeAt(secret, 30)));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private HttpClient? _client;
    private Guid _userId;

    private async Task<(string Secret, List<string> RecoveryCodes)> EnableTwoFactorAsync()
    {
        (_client, _userId) = await CreateUserAndClientAsync(UserRole.USER, Username);

        HttpResponseMessage setup = await SetupAsync(_client!);
        TwoFactorSetupApiResult secretResult = (await setup.Content.ReadFromJsonAsync<TwoFactorSetupApiResult>())!;
        secretResult.OtpAuthUri.ShouldStartWith("otpauth://totp/");

        HttpResponseMessage enable = await _client.PostAsJsonAsync("/users/account/2fa/enable",
            new TwoFactorCodeRequest { Code = CodeAt(secretResult.Secret, 0) });
        enable.StatusCode.ShouldBe(HttpStatusCode.OK);
        TwoFactorEnabledApiResult enabled = (await enable.Content.ReadFromJsonAsync<TwoFactorEnabledApiResult>())!;

        // Enabling revokes the previous token, continue with the session the response issued.
        _client.WithBearerToken(GetSessionToken(enable));

        return (secretResult.Secret, enabled.RecoveryCodes);
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

    private async Task<bool> IsTwoFactorEnabledAsync(Guid userId)
    {
        bool enabled = false;
        await WithDbContext(async db => enabled = (await db.Users.AsNoTracking().FirstAsync(x => x.Id == userId)).TotpEnabled);
        return enabled;
    }

    private Task<HttpResponseMessage> TwoFactorLoginFromAsync(string challenge, string code, string clientIp)
    {
        HttpRequestMessage message = new(HttpMethod.Post, "/auth/login/2fa")
        {
            Content = JsonContent.Create(new TwoFactorLoginRequest { Challenge = challenge, Code = code })
        };
        message.Headers.Add("X-Real-IP", clientIp);

        return CreateClient().SendAsync(message);
    }

    // Enables two-factor for a second user and returns a login challenge of that user.
    private async Task<string> EnableTwoFactorForOtherUserAsync()
    {
        const string otherUsername = "other-2fa-user";
        (HttpClient client, _) = await CreateUserAndClientAsync(UserRole.USER, otherUsername);

        TwoFactorSetupApiResult setup = (await (await SetupAsync(client)).Content.ReadFromJsonAsync<TwoFactorSetupApiResult>())!;
        HttpResponseMessage enable = await client.PostAsJsonAsync("/users/account/2fa/enable", new TwoFactorCodeRequest { Code = CodeAt(setup.Secret, 0) });
        enable.StatusCode.ShouldBe(HttpStatusCode.OK);

        HttpResponseMessage login = await CreateClient().PostAsJsonAsync("/auth/login", new LoginRequest { Username = otherUsername, Password = Password });
        return (await login.Content.ReadFromJsonAsync<LoginTwoFactorRequiredApiResult>())!.Challenge;
    }

    private static Task<HttpResponseMessage> SetupAsync(HttpClient client, string password = Password)
    {
        return client.PostAsJsonAsync("/users/account/2fa/setup", new TwoFactorPasswordRequest { Password = password });
    }

    private async Task<List<UserRecoveryCodeEntity>> GetRecoveryCodesAsync()
    {
        List<UserRecoveryCodeEntity> codes = new();
        await WithDbContext(async db => codes = await db.UserRecoveryCodes.AsNoTracking().Where(x => x.UserId == _userId).ToListAsync());
        return codes;
    }

    private async Task<string> GetChallengeAsync()
    {
        HttpResponseMessage response = await PasswordLoginAsync();
        return (await response.Content.ReadFromJsonAsync<LoginTwoFactorRequiredApiResult>())!.Challenge;
    }

    private Task<HttpResponseMessage> PasswordLoginAsync()
    {
        return CreateClient().PostAsJsonAsync("/auth/login", new LoginRequest { Username = Username, Password = Password });
    }

    private Task<HttpResponseMessage> TwoFactorLoginAsync(string challenge, string code)
    {
        return CreateClient().PostAsJsonAsync("/auth/login/2fa", new TwoFactorLoginRequest { Challenge = challenge, Code = code });
    }

    private async Task<T> WithTimeOffsetAsync<T>(int seconds, Func<Task<T>> action)
    {
        DateTimeOffset originalTime = Factory.FakeTime.GetUtcNow();
        try
        {
            Factory.FakeTime.SetUtcNow(originalTime.AddSeconds(seconds));
            return await action();
        }
        finally
        {
            Factory.FakeTime.SetUtcNow(originalTime);
        }
    }

    // Independent RFC 6238 implementation, so the tests do not only mirror the production code.
    private string CodeAt(string secret, int offsetSeconds)
    {
        long step = (Factory.FakeTime.GetUtcNow().ToUnixTimeSeconds() + offsetSeconds) / 30;
        byte[] counter = BitConverter.GetBytes(step);
        Array.Reverse(counter);

        using HMACSHA1 hmac = new(DecodeBase32(secret));
        byte[] hash = hmac.ComputeHash(counter);
        int offset = hash[^1] & 0x0F;
        int binary = ((hash[offset] & 0x7F) << 24) | (hash[offset + 1] << 16) | (hash[offset + 2] << 8) | hash[offset + 3];
        return (binary % 1_000_000).ToString("D6");
    }

    private static byte[] DecodeBase32(string text)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        List<byte> bytes = new();
        int buffer = 0;
        int bits = 0;
        foreach (char c in text)
        {
            buffer = (buffer << 5) | alphabet.IndexOf(c);
            bits += 5;
            if (bits >= 8)
            {
                bytes.Add((byte)((buffer >> (bits - 8)) & 0xFF));
                bits -= 8;
            }
        }

        return bytes.ToArray();
    }
}
