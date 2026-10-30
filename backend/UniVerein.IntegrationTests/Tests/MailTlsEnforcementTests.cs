using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Shouldly;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.Exceptions;
using UniVerein.Api.Models;
using UniVerein.Api.Services;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using UniVerein.IntegrationTests.Infrastructure;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

// The fake mail server never offers STARTTLS but advertises AUTH PLAIN/LOGIN, like a server an attacker stripped
// the encryption from. It records every line it receives, so the tests can prove that the password never arrives.
[Collection("NonParallelTests")]
public class MailTlsEnforcementTests : IntegrationTestBase
{
    private const string Password = "S3cret-Mailbox-Password";

    private readonly CryptoService _cryptoService;
    private FakeSmtpServer _server = null!;

    public MailTlsEnforcementTests(UniVereinWebApplicationFactory factory) : base(factory)
    {
        _cryptoService = GetService<CryptoService>();
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await ClearMailSettingsAsync();
        _server = new FakeSmtpServer();
    }

    public override async Task DisposeAsync()
    {
        _server.Dispose();
        await ClearMailSettingsAsync();
    }

    [Fact]
    public async Task SendTestMail_ServerWithoutStartTls_ReturnsTlsErrorAndNeverSendsPassword()
    {
        // Arrange
        await CreateMailSettingsAsync(enableSsl: true);
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/mail/test",
            new TestMailRequest { Email = "test@test.de" });
        JsonObject? error = await response.Content.ReadFromJsonAsync<JsonObject>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        error.ShouldNotBeNull();
        error["errorCode"]!.GetValue<string>().ShouldBe(ApiErrorCodes.MAIL_TLS_UNAVAILABLE);
        await AssertPasswordNeverSentAsync();
    }

    [Fact]
    public async Task SendEmails_ServerWithoutStartTls_NeverSendsPassword()
    {
        // Arrange
        await CreateMailSettingsAsync(enableSsl: true);

        // Act
        await SendEmailsAsync();

        // Assert
        await AssertPasswordNeverSentAsync();
        _server.ReceivedLines.ShouldNotContain(l => l.StartsWith("MAIL FROM", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SendEmails_EncryptionDisabled_SendsPasswordInCleartext()
    {
        // Arrange: control case, proves that the fake server would have seen the password.
        await CreateMailSettingsAsync(enableSsl: false);

        // Act
        await SendEmailsAsync();

        // Assert
        await WaitUntilAsync(() => _server.ReceivedLines.Any(IsAuthLine));
        _server.ReceivedLines.ShouldContain(l => IsAuthLine(l));
    }

    private static bool IsAuthLine(string line)
    {
        return line.StartsWith("AUTH", StringComparison.OrdinalIgnoreCase);
    }

    private async Task SendEmailsAsync()
    {
        Mock<ISingleClientProxy> clientProxy = new();
        clientProxy
            .Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        Mock<IHubClients> hubClients = new();
        hubClients.Setup(c => c.Client(It.IsAny<string>())).Returns(clientProxy.Object);
        Mock<IHubContext<EmailProgressHub>> hubContext = new();
        hubContext.Setup(h => h.Clients).Returns(hubClients.Object);

        await WithDbContext(async db =>
        {
            await new MailService(db, _cryptoService, hubContext.Object, new FakeImapClient()).SendEmailsAsync(
                [new Recipient { Email = "test@tester.de", FirstName = "Tester", LastName = "Tester" }],
                new EmailRequest { Subject = "Subject", HtmlBody = "Body" }, "123456789");
        });
    }

    private async Task AssertPasswordNeverSentAsync()
    {
        // Give a (wrongly) connected client the chance to deliver its AUTH line first.
        await Task.Delay(500);

        string[] lines = _server.ReceivedLines.ToArray();
        lines.ShouldNotContain(l => IsAuthLine(l));
        string base64Password = Convert.ToBase64String(Encoding.ASCII.GetBytes(Password));
        lines.ShouldNotContain(l => l.Contains(Password) || l.Contains(base64Password));
    }

    private async Task CreateMailSettingsAsync(bool enableSsl)
    {
        await WithDbContext(async db =>
        {
            await db.MailSettings.AddAsync(new MailSettingsEntity
            {
                SmtpServer = "127.0.0.1",
                Port = _server.Port,
                Username = "mailbox-user",
                Password = _cryptoService.Encrypt(Password),
                FromMail = "noreply@test.de",
                FromName = "Vorstand Test",
                EnableSsl = enableSsl
            });
            await db.SaveChangesAsync();
        });
    }

    private async Task ClearMailSettingsAsync()
    {
        await WithDbContext(async db =>
        {
            db.MailSettings.RemoveRange(db.MailSettings.AsQueryable());
            await db.ForceSaveChangesAsync();
        });
    }

    private static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (!condition() && DateTime.UtcNow < deadline)
            await Task.Delay(50);
    }
}
