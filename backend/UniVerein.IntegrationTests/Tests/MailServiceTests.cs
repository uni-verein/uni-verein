using System.Diagnostics;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.Models;
using UniVerein.Api.Services;
using UniVerein.DAL.Entities;
using UniVerein.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using MimeKit;
using Moq;
using SmtpServer;
using SmtpServer.Storage;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

[Collection("NonParallelTests")]
public class MailServiceTests : IntegrationTestBase
{
    private SmtpServer.SmtpServer _smtpServer = null!;
    private CancellationTokenSource _cts = null!;
    private readonly List<MimeMessage> _receivedMails = new();

    public MailServiceTests(UniVereinWebApplicationFactory factory) : base(factory)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        var options = new SmtpServerOptionsBuilder()
            .ServerName("localhost")
            .Port(2521)
            .Build();

        var serviceProvider = new ServiceCollection()
            .AddSingleton<IMessageStore>(new TestMessageStore(_receivedMails))
            .BuildServiceProvider();

        _smtpServer = new SmtpServer.SmtpServer(options, serviceProvider);
        _cts = new CancellationTokenSource();
        Task startTask = _smtpServer.StartAsync(_cts.Token);
        await Task.WhenAny(startTask, Task.Delay(500));

        if (startTask.IsFaulted)
            throw new Exception("SMTP-Server not started", startTask.Exception);
    }

    public override Task DisposeAsync()
    {
        _cts.Cancel();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task SendToAll_ShouldDeliverToAllMembers()
    {
        // Arrange
        List<Recipient> recipients = new()
        {
            new Recipient()
            {
                Email = "test@tester.de",
                FirstName = "Tester",
                LastName = "Tester"
            }
        };
        EmailRequest request = new EmailRequest()
        {
            Subject = "Subject",
            HtmlBody = "Body",
            Attachments = new()
            {
                new() { Base64Content = "", FileName = "test.txt", ContentType = "text/plain" }
            }
        };
        CryptoService cryptoService = GetService<CryptoService>();
        Mock<IHubClients> mockClients = new Mock<IHubClients>();
        Mock<ISingleClientProxy> mockClientProxy = new Mock<ISingleClientProxy>();
        mockClientProxy
            .Setup(p => p.SendCoreAsync(
                It.IsAny<string>(),
                It.IsAny<object[]>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        mockClients.Setup(c => c.All).Returns(mockClientProxy.Object);
        mockClients.Setup(c => c.Client(It.IsAny<string>())).Returns(mockClientProxy.Object);
        Mock<IHubContext<EmailProgressHub>> mockHubContext = new Mock<IHubContext<EmailProgressHub>>();
        mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);
        FakeImapClient fakeClient = new FakeImapClient();

        await WithDbContext(async db =>
        {
            await db.MailSettings.AddAsync(new MailSettingsEntity()
            {
                SmtpServer = "localhost",
                Port = 2521,
                ImapServer = "localhost",
                ImapPort = 2521,
                Username = "test",
                Password = cryptoService.Encrypt("test"),
                FromMail = "noreply@test.de",
                EnableSsl = false
            });
            Guid memberId = Guid.NewGuid();
            await db.Members.AddAsync(new MemberEntity()
            {
                MandateId = memberId.ToString(),
                Id = memberId,
                FirstName = "John",
                LastName = "Doe",
                BirthdayEncrypted = string.Empty,
                EmailEncrypted = cryptoService.Encrypt("test@test.de"),
                StreetEncrypted = string.Empty,
                PostalCode =  "12345",
                CountryCode =  "DE",
                City = string.Empty,
            });
            await db.SaveChangesAsync();

            // Act
            await new MailService(db, cryptoService, mockHubContext.Object, fakeClient).SendEmailsAsync(recipients,
                request, "123456789");
        });

        await WaitForConditionAsync(
            condition: () => _receivedMails.Count >= 1 && fakeClient.AppendedMessages.Count >= 1,
            timeoutMs: 5000,
            intervalMs: 100
        );

        // Assert
        Assert.Single(_receivedMails);
        Assert.All(_receivedMails, m => Assert.Equal("Subject", m.Subject));
        Assert.Single(fakeClient.AppendedMessages);
        Assert.Equal("Sent", fakeClient.AppendedMessages[0].Folder);
    }

    private static async Task WaitForConditionAsync(Func<bool> condition, int timeoutMs = 5000, int intervalMs = 100)
    {
        TimeSpan timeout = TimeSpan.FromMilliseconds(timeoutMs);
        TimeSpan interval = TimeSpan.FromMilliseconds(intervalMs);
        Stopwatch stopwatch = Stopwatch.StartNew();

        while (!condition())
        {
            if (stopwatch.Elapsed > timeout)
                throw new TimeoutException(
                    $"Condition not met within {timeoutMs}ms. The SMTP server may not have received the message in time.");

            await Task.Delay(interval);
        }
    }
}