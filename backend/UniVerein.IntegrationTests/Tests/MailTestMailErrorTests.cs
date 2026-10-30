using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Shouldly;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.Exceptions;
using UniVerein.Api.Services;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using UniVerein.IntegrationTests.Infrastructure;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

[Collection("NonParallelTests")]
public class MailTestMailErrorTests : IntegrationTestBase
{
    private readonly CryptoService _cryptoService;
    private FakeSmtpServer _server = null!;

    public MailTestMailErrorTests(UniVereinWebApplicationFactory factory) : base(factory)
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
    public async Task SendTestMail_HostNotReachable_ReturnsConnectionFailed()
    {
        // Arrange
        await CreateMailSettingsAsync(FakeSmtpServer.GetClosedPort());

        // Act
        (HttpStatusCode status, string? errorCode) = await SendTestMailAsync();

        // Assert
        status.ShouldBe(HttpStatusCode.UnprocessableEntity);
        errorCode.ShouldBe(ApiErrorCodes.MAIL_CONNECTION_FAILED);
    }

    [Fact]
    public async Task SendTestMail_LoginRejected_ReturnsAuthFailed()
    {
        // Arrange
        _server.RejectAuthentication = true;
        await CreateMailSettingsAsync(_server.Port);

        // Act
        (HttpStatusCode status, string? errorCode) = await SendTestMailAsync();

        // Assert
        status.ShouldBe(HttpStatusCode.UnprocessableEntity);
        errorCode.ShouldBe(ApiErrorCodes.MAIL_AUTH_FAILED);
    }

    [Fact]
    public async Task SendTestMail_SenderRejected_ReturnsSendFailed()
    {
        // Arrange
        _server.RejectMailFrom = true;
        await CreateMailSettingsAsync(_server.Port);

        // Act
        (HttpStatusCode status, string? errorCode) = await SendTestMailAsync();

        // Assert
        status.ShouldBe(HttpStatusCode.UnprocessableEntity);
        errorCode.ShouldBe(ApiErrorCodes.MAIL_SEND_FAILED);
    }

    [Fact]
    public async Task SendTestMail_ServerAcceptsMail_ReturnsOk()
    {
        // Arrange
        await CreateMailSettingsAsync(_server.Port);

        // Act
        (HttpStatusCode status, string? errorCode) = await SendTestMailAsync();

        // Assert
        status.ShouldBe(HttpStatusCode.OK);
        errorCode.ShouldBeNull();
        _server.ReceivedLines.ShouldContain(l => l.StartsWith("MAIL FROM", StringComparison.OrdinalIgnoreCase));
    }

    private async Task<(HttpStatusCode Status, string? ErrorCode)> SendTestMailAsync()
    {
        HttpClient client = CreateClient(UserRole.ADMIN);

        HttpResponseMessage response = await client.PostAsJsonAsync("/mail/test",
            new TestMailRequest { Email = "test@test.de" });
        if (response.IsSuccessStatusCode)
            return (response.StatusCode, null);

        JsonObject? error = await response.Content.ReadFromJsonAsync<JsonObject>();
        return (response.StatusCode, error?["errorCode"]?.GetValue<string>());
    }

    private async Task CreateMailSettingsAsync(int port)
    {
        await WithDbContext(async db =>
        {
            await db.MailSettings.AddAsync(new MailSettingsEntity
            {
                SmtpServer = "127.0.0.1",
                Port = port,
                Username = "mailbox-user",
                Password = _cryptoService.Encrypt("mailbox-password"),
                FromMail = "noreply@test.de",
                FromName = "Vorstand Test",
                EnableSsl = false
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
}
