using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MimeKit;
using OtpNet;
using Shouldly;
using SmtpServer;
using SmtpServer.Storage;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Services;
using UniVerein.Api.Services.LoginNotification;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using UniVerein.IntegrationTests.Infrastructure;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

[Collection("NonParallelTests")]
public class NewDeviceLoginNotificationTests : IntegrationTestBase
{
    private const int SmtpPort = 2528;
    private const string Username = "anna";
    private const string Password = "Test1234!";
    private const string Email = "anna@test.de";
    private const string ChromeOnWindows =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36";
    private const string SafariOnIphone =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1";
    private const string FirefoxOnLinux = "Mozilla/5.0 (X11; Linux x86_64; rv:132.0) Gecko/20100101 Firefox/132.0";

    private SmtpServer.SmtpServer _smtpServer = null!;
    private CancellationTokenSource _cts = null!;
    private readonly List<MimeMessage> _receivedMails = new();
    private CryptoService _crypto = null!;
    private Guid _userId;

    public NewDeviceLoginNotificationTests(UniVereinWebApplicationFactory factory) : base(factory)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _crypto = GetService<CryptoService>();

        await WithDbContext(async db =>
        {
            await db.MailSettings.AddAsync(new MailSettingsEntity
            {
                SmtpServer = "127.0.0.1",
                Port = SmtpPort,
                Username = "test",
                Password = _crypto.Encrypt("test"),
                FromMail = "noreply@test.de",
                EnableSsl = false
            });
            await db.SaveChangesAsync();
        });

        (_, _userId) = await CreateUserAndClientAsync(UserRole.USER, Username);
        await WithDbContext(async db =>
        {
            UserEntity user = await db.Users.SingleAsync(u => u.Id == _userId);
            user.Email = _crypto.Encrypt(Email);
            await db.SaveChangesAsync();
        });

        ISmtpServerOptions options = new SmtpServerOptionsBuilder().ServerName("localhost").Port(SmtpPort).Build();
        ServiceProvider serviceProvider = new ServiceCollection()
            .AddSingleton<IMessageStore>(new TestMessageStore(_receivedMails))
            .BuildServiceProvider();

        _smtpServer = new SmtpServer.SmtpServer(options, serviceProvider);
        _cts = new CancellationTokenSource();
        Task startTask = _smtpServer.StartAsync(_cts.Token);
        await WaitForSmtpPortAsync(startTask);
    }

    private static async Task WaitForSmtpPortAsync(Task startTask)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(15);
        while (true)
        {
            if (startTask.IsFaulted)
                throw new Exception("SMTP-Server not started", startTask.Exception);

            try
            {
                using System.Net.Sockets.TcpClient probe = new();
                await probe.ConnectAsync("127.0.0.1", SmtpPort);
                return;
            }
            catch (System.Net.Sockets.SocketException) when (DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(50);
            }
        }
    }

    public override async Task DisposeAsync()
    {
        _cts.Cancel();
        await base.DisposeAsync();
    }

    private static async Task<(HttpResponseMessage Response, string? DeviceCookie)> SendAsync(HttpClient client,
        string path, object body, string? deviceCookie, string ip, string userAgent)
    {
        HttpRequestMessage message = new(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        message.Headers.Add("X-Real-IP", ip);
        message.Headers.TryAddWithoutValidation("User-Agent", userAgent);
        if (deviceCookie != null)
            message.Headers.Add("Cookie", $"uv_device={deviceCookie}");

        HttpResponseMessage response = await client.SendAsync(message);
        string? newCookie = response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? cookies)
            ? cookies.FirstOrDefault(c => c.StartsWith("uv_device=", StringComparison.Ordinal))?.Split(';')[0]["uv_device=".Length..]
            : null;
        return (response, newCookie ?? deviceCookie);
    }

    private Task<(HttpResponseMessage Response, string? DeviceCookie)> LoginAsync(string? deviceCookie,
        string ip = "203.0.113.7", string userAgent = ChromeOnWindows, string password = Password)
    {
        return SendAsync(CreateClient(), "/auth/login", new LoginRequest { Username = Username, Password = password },
            deviceCookie, ip, userAgent);
    }

    private async Task WaitForMailsAsync(int count)
    {
        await WaitForConditionAsync(() => MailCount() >= count);
        await Task.Delay(300);
    }

    private int MailCount()
    {
        lock (_receivedMails)
            return _receivedMails.Count;
    }

    private async Task<int> MailCountAfterGracePeriodAsync()
    {
        await Task.Delay(1000);
        return MailCount();
    }

    private void AdvancePastMailInterval()
    {
        Factory.FakeTime.SetUtcNow(Factory.FakeTime.GetUtcNow() + LoginNotificationQueue.MailInterval + TimeSpan.FromMinutes(1));
    }

    private async Task<int> DeviceCountAsync()
    {
        int count = 0;
        await WithDbContext(async db => count = await db.UserDevices.CountAsync(d => d.UserId == _userId));
        return count;
    }

    [Fact]
    public async Task FirstLogin_RegistersDeviceAndSetsCookie_WithoutMail()
    {
        // Act
        (HttpResponseMessage response, string? cookie) = await LoginAsync(null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        cookie.ShouldNotBeNull();
        string setCookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("uv_device=", StringComparison.Ordinal));
        setCookie.ShouldContain("httponly", Case.Insensitive);
        setCookie.ShouldContain("samesite=strict", Case.Insensitive);
        (await DeviceCountAsync()).ShouldBe(1);
        (await MailCountAfterGracePeriodAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task LoginFromNewDevice_SendsMailWithDeviceAndIp()
    {
        // Arrange
        await LoginAsync(null);

        // Act
        (HttpResponseMessage response, _) = await LoginAsync(null, ip: "198.51.100.23", userAgent: SafariOnIphone);
        await WaitForMailsAsync(1);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        MimeMessage mail = _receivedMails.Single();
        mail.To.ToString().ShouldContain(Email);
        mail.Subject.ShouldNotBeNull().ShouldContain("neuem Gerät");
        string html = mail.HtmlBody.ShouldNotBeNull();
        html.ShouldContain("Safari (iOS)");
        html.ShouldContain("198.51.100.23");
        html.ShouldContain(Username);
        html.ShouldNotContain("zweite Faktor steht noch aus");
        (await DeviceCountAsync()).ShouldBe(2);
    }

    [Fact]
    public async Task LoginFromKnownDevice_SendsNoMail()
    {
        // Arrange
        (_, string? cookie) = await LoginAsync(null);

        // Act
        await LoginAsync(cookie);
        await LoginAsync(cookie);

        // Assert
        (await MailCountAfterGracePeriodAsync()).ShouldBe(0);
        (await DeviceCountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Logout_DoesNotMakeTheDeviceUnknown()
    {
        // Arrange
        (HttpResponseMessage first, string? cookie) = await LoginAsync(null);
        string token = (await first.Content.ReadFromJsonAsync<LoginApiResult>())!.Token;
        HttpClient authenticated = CreateClient().WithBearerToken(token);

        // Act
        HttpResponseMessage logout = await authenticated.PostAsync("/auth/logout", null);
        (HttpResponseMessage second, string? cookieAfter) = await LoginAsync(cookie);

        // Assert
        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        logout.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? setCookies);
        (setCookies ?? []).ShouldNotContain(c => c.StartsWith("uv_device=", StringComparison.Ordinal));
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        cookieAfter.ShouldBe(cookie);
        (await DeviceCountAsync()).ShouldBe(1);
        (await MailCountAfterGracePeriodAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task SessionRevocation_DoesNotMakeTheDeviceUnknown()
    {
        // Arrange
        (_, string? cookie) = await LoginAsync(null);
        await WithDbContext(async db =>
        {
            UserEntity user = await db.Users.SingleAsync(u => u.Id == _userId);
            user.SessionVersion++;
            await db.SaveChangesAsync();
        });

        // Act
        await LoginAsync(cookie);

        // Assert
        (await MailCountAfterGracePeriodAsync()).ShouldBe(0);
        (await DeviceCountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task MultipleDevices_OnlyTheFirstLoginOfEachDeviceSendsMail()
    {
        // Arrange
        (_, string? pc) = await LoginAsync(null, userAgent: ChromeOnWindows);

        // Act
        (_, string? tablet) = await LoginAsync(null, userAgent: SafariOnIphone);
        await WaitForMailsAsync(1);
        AdvancePastMailInterval();
        (_, string? laptop) = await LoginAsync(null, userAgent: FirefoxOnLinux);
        await WaitForMailsAsync(2);
        AdvancePastMailInterval();

        await LoginAsync(pc, userAgent: ChromeOnWindows);
        await LoginAsync(tablet, userAgent: SafariOnIphone);
        await LoginAsync(laptop, userAgent: FirefoxOnLinux);

        // Assert
        (await MailCountAfterGracePeriodAsync()).ShouldBe(2);
        (await DeviceCountAsync()).ShouldBe(3);
        new[] { pc, tablet, laptop }.Distinct().Count().ShouldBe(3);
    }

    [Fact]
    public async Task WrongPassword_SendsNoMail()
    {
        // Arrange
        await LoginAsync(null);

        // Act
        (HttpResponseMessage response, _) = await LoginAsync(null, password: "wrong-password");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await MailCountAfterGracePeriodAsync()).ShouldBe(0);
        (await DeviceCountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task RepeatedAttemptsFromNewDevice_SendOnlyOneMail()
    {
        // Arrange
        await LoginAsync(null);

        // Act
        await LoginAsync(null);
        await LoginAsync(null);
        await LoginAsync(null);

        // Assert
        (await MailCountAfterGracePeriodAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task LoginFromNewDevice_WithoutEmailAddress_StillWorksAndSendsNoMail()
    {
        // Arrange
        await WithDbContext(async db =>
        {
            UserEntity user = await db.Users.SingleAsync(u => u.Id == _userId);
            user.Email = null;
            await db.SaveChangesAsync();
        });
        await LoginAsync(null);

        // Act
        (HttpResponseMessage response, _) = await LoginAsync(null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await MailCountAfterGracePeriodAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task LoginFromNewDevice_WithoutMailSettings_StillWorks()
    {
        // Arrange
        await WithDbContext(async db =>
        {
            db.MailSettings.RemoveRange(db.MailSettings.IgnoreQueryFilters());
            await db.SaveChangesAsync();
        });
        await LoginAsync(null);

        // Act
        (HttpResponseMessage response, _) = await LoginAsync(null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await MailCountAfterGracePeriodAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task LoginFromNewDevice_EncodesUserAgentAndIpInMail()
    {
        // Arrange
        await LoginAsync(null);

        // Act
        await LoginAsync(null, userAgent: "<script>alert(1)</script> {fullname} {firstname}");
        await WaitForMailsAsync(1);

        // Assert
        string html = _receivedMails.Single().HtmlBody.ShouldNotBeNull();
        html.ShouldNotContain("<script>");
        html.ShouldContain("&lt;script&gt;alert(1)&lt;/script&gt;");
        html.ShouldNotContain("{fullname}");
        html.ShouldNotContain("{firstname}");
    }

    [Fact]
    public async Task TwoFactor_SendsMailAfterPasswordStep_AndRegistersDeviceOnlyAfterSecondFactor()
    {
        // Arrange
        string secret = TwoFactorService.GenerateSecret();
        await LoginAsync(null);
        await WithDbContext(async db =>
        {
            UserEntity user = await db.Users.SingleAsync(u => u.Id == _userId);
            user.TotpEnabled = true;
            user.TotpSecret = GetService<TwoFactorService>().EncryptSecret(secret);
            await db.SaveChangesAsync();
        });

        // Act
        (HttpResponseMessage passwordStep, string? cookieAfterPassword) = await LoginAsync(null, ip: "198.51.100.23");
        await WaitForMailsAsync(1);
        int devicesAfterPassword = await DeviceCountAsync();

        LoginTwoFactorRequiredApiResult challenge = (await passwordStep.Content.ReadFromJsonAsync<LoginTwoFactorRequiredApiResult>())!;
        string code = new Totp(Base32Encoding.ToBytes(secret)).ComputeTotp(Factory.FakeTime.GetUtcNow().UtcDateTime);
        (HttpResponseMessage secondStep, string? cookieAfterSecondFactor) = await SendAsync(CreateClient(), "/auth/login/2fa",
            new TwoFactorLoginRequest { Challenge = challenge.Challenge, Code = code }, null, "198.51.100.23", ChromeOnWindows);

        // Assert
        passwordStep.StatusCode.ShouldBe(HttpStatusCode.OK);
        cookieAfterPassword.ShouldBeNull();
        devicesAfterPassword.ShouldBe(1);
        string html = _receivedMails.Single().HtmlBody.ShouldNotBeNull();
        html.ShouldContain("zweite Faktor steht noch aus");
        html.ShouldContain("198.51.100.23");

        secondStep.StatusCode.ShouldBe(HttpStatusCode.OK);
        cookieAfterSecondFactor.ShouldNotBeNull();
        (await DeviceCountAsync()).ShouldBe(2);
        (await MailCountAfterGracePeriodAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task TwoFactor_WithoutCompletingSecondFactor_NeverTrustsTheDevice()
    {
        // Arrange
        await LoginAsync(null);
        await WithDbContext(async db =>
        {
            UserEntity user = await db.Users.SingleAsync(u => u.Id == _userId);
            user.TotpEnabled = true;
            user.TotpSecret = GetService<TwoFactorService>().EncryptSecret(TwoFactorService.GenerateSecret());
            await db.SaveChangesAsync();
        });

        // Act
        (_, string? cookie) = await LoginAsync(null);
        await WaitForMailsAsync(1);
        AdvancePastMailInterval();
        await LoginAsync(cookie);
        await WaitForMailsAsync(2);

        // Assert
        (await DeviceCountAsync()).ShouldBe(1);
    }

    private static async Task WaitForConditionAsync(Func<bool> condition, int timeoutMs = 5000, int intervalMs = 100)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition())
        {
            if (DateTimeOffset.UtcNow > deadline)
                throw new TimeoutException($"Condition not met within {timeoutMs}ms.");
            await Task.Delay(intervalMs);
        }
    }
}
