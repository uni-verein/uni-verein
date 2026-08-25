using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using UniVerein.Api.Services;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using UniVerein.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MimeKit;
using Moq;
using Shouldly;
using SmtpServer;
using SmtpServer.Storage;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

[Collection("NonParallelTests")]
public class ReceiptNotificationServiceTests : IntegrationTestBase
{
    private SmtpServer.SmtpServer _smtpServer = null!;
    private CancellationTokenSource _cts = null!;
    private readonly List<MimeMessage> _receivedMails = new();
    private CryptoService _cryptoService = null!;

    public ReceiptNotificationServiceTests(UniVereinWebApplicationFactory factory) : base(factory)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _cryptoService = GetService<CryptoService>();

        await WithDbContext(async db =>
        {
            db.UserSettings.RemoveRange(db.UserSettings.AsQueryable());
            db.Receipts.RemoveRange(db.Receipts.AsQueryable());
            await db.ForceSaveChangesAsync();
        });

        ISmtpServerOptions options = new SmtpServerOptionsBuilder()
            .ServerName("localhost")
            .Port(2523)
            .Build();

        ServiceProvider serviceProvider = new ServiceCollection()
            .AddSingleton<IMessageStore>(new TestMessageStore(_receivedMails))
            .BuildServiceProvider();

        _smtpServer = new SmtpServer.SmtpServer(options, serviceProvider);
        _cts = new CancellationTokenSource();
        Task startTask = _smtpServer.StartAsync(_cts.Token);
        await Task.WhenAny(startTask, Task.Delay(500));

        if (startTask.IsFaulted)
            throw new Exception("SMTP-Server not started", startTask.Exception);
    }

    public override async Task DisposeAsync()
    {
        _cts.Cancel();
        await WithDbContext(async db =>
        {
            db.UserSettings.RemoveRange(db.UserSettings.AsQueryable());
            db.Receipts.RemoveRange(db.Receipts.AsQueryable());
            await db.ForceSaveChangesAsync();
        });
    }

    // ---------------------------------------------------------------
    // Full HTTP path: POST /receipts
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.ADMIN)]
    public async Task CreateAsync_ByUserOrAdmin_NotifiesOptedInFinancialManager(UserRole creatorRole)
    {
        // Arrange
        await SetMailSettingsAsync();
        (HttpClient creatorClient, _) = await CreateUserAndClientAsync(creatorRole, "creator");
        await SetReceiptNotificationSettingAsync(role: UserRole.FINANCIAL_MANAGER, enabled: true,
            email: "opted-in-fm@test.de");

        // Act
        await CreateReceiptAsync(creatorClient);

        // Assert
        await WaitForConditionAsync(() => _receivedMails.Count >= 1);
        _receivedMails.Count.ShouldBe(1);
        _receivedMails[0].To.ToString().ShouldContain("opted-in-fm@test.de");
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.ADMIN)]
    public async Task CreateAsync_FinancialManagerWithoutSettingsRow_IsNotifiedByDefault(UserRole creatorRole)
    {
        // Arrange
        await SetMailSettingsAsync();
        (HttpClient creatorClient, _) = await CreateUserAndClientAsync(creatorRole, "creator");
        UserEntity user = await CreateUser(UserRole.FINANCIAL_MANAGER, "default-on-fm@test.de");

        // Act
        await CreateReceiptAsync(creatorClient);

        // Assert
        await WaitForConditionAsync(() => _receivedMails.Count >= 1);
        _receivedMails.Count.ShouldBe(1);
        _receivedMails[0].To.ToString().ShouldContain("default-on-fm@test.de");

        await WithDbContext(async db =>
        {
            (await db.UserSettings.AnyAsync(s => s.UserId == user.Id)).ShouldBeFalse();
        });
    }

    [Fact]
    public async Task CreateAsync_ByFinancialManager_NotifiesNobody()
    {
        // Arrange
        await SetMailSettingsAsync();
        (HttpClient creatorClient, _) = await CreateUserAndClientAsync(UserRole.FINANCIAL_MANAGER, "creator-fm");
        await SetReceiptNotificationSettingAsync(role: UserRole.FINANCIAL_MANAGER, enabled: true,
            email: "other-fm@test.de");

        // Act
        await CreateReceiptAsync(creatorClient);
        await Task.Delay(500);

        // Assert
        _receivedMails.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateAsync_FinancialManagerSettingDisabled_DoesNotNotify()
    {
        // Arrange
        await SetMailSettingsAsync();
        (HttpClient creatorClient, _) = await CreateUserAndClientAsync(UserRole.USER, "creator");
        await SetReceiptNotificationSettingAsync(role: UserRole.FINANCIAL_MANAGER, enabled: false,
            email: "disabled-fm@test.de");

        // Act
        await CreateReceiptAsync(creatorClient);
        await Task.Delay(500);

        // Assert
        _receivedMails.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateAsync_NoMailSettingsConfigured_ReceiptStillCreated()
    {
        // Arrange
        (HttpClient creatorClient, _) = await CreateUserAndClientAsync(UserRole.USER, "creator");
        await SetReceiptNotificationSettingAsync(role: UserRole.FINANCIAL_MANAGER, enabled: true,
            email: "fm@test.de");

        // Act
        HttpResponseMessage response = await CreateReceiptAsync(creatorClient);
        await Task.Delay(500);

        // Assert
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.Created);
        _receivedMails.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateAsync_MultipleOptedInFinancialManagers_AllNotified()
    {
        // Arrange
        await SetMailSettingsAsync();
        (HttpClient creatorClient, _) = await CreateUserAndClientAsync(UserRole.USER, "creator");
        await SetReceiptNotificationSettingAsync(role: UserRole.FINANCIAL_MANAGER, enabled: true, email: "fm1@test.de");
        await SetReceiptNotificationSettingAsync(role: UserRole.FINANCIAL_MANAGER, enabled: true, email: "fm2@test.de");

        // Act
        await CreateReceiptAsync(creatorClient);

        // Assert
        await WaitForConditionAsync(() => _receivedMails.Count >= 2);
        _receivedMails.Count.ShouldBe(2);
    }

    // ---------------------------------------------------------------
    // Direct service edge cases
    // ---------------------------------------------------------------

    [Fact]
    public async Task NotifyFinancialManagersAsync_RecipientHasNoEmail_SkipsSilentlyAndNotifiesOthers()
    {
        // Arrange
        await SetMailSettingsAsync();

        UserEntity creator = await CreateUser(UserRole.USER);
        UserEntity fmWithoutEmail = await CreateUser(UserRole.FINANCIAL_MANAGER);
        UserEntity fmWithEmail = await CreateUser(UserRole.FINANCIAL_MANAGER, "has-email@test.de");

        await WithDbContext(async db =>
        {
            db.Attach(fmWithoutEmail);
            db.Attach(fmWithEmail);

            await db.UserSettings.AddRangeAsync(
                new UserSettingEntity { UserId = fmWithoutEmail.Id, User = fmWithoutEmail, Type = UserSettingType.RECEIPT_NOTIFICATION, Enabled = true },
                new UserSettingEntity { UserId = fmWithEmail.Id, User = fmWithEmail, Type = UserSettingType.RECEIPT_NOTIFICATION, Enabled = true });
            await db.SaveChangesAsync();

            ReceiptEntity receipt = new()
            {
                Id = Guid.NewGuid(),
                UserId = creator.Id,
                Amount = 5,
                ReceiptDate = DateTime.UtcNow
            };
            await db.Receipts.AddAsync(receipt);
            await db.SaveChangesAsync();

            ReceiptNotificationService service = BuildReceiptNotificationService(db);

            // Act
            await service.NotifyFinancialManagersAsync(receipt, creator);
        });

        // Assert
        await WaitForConditionAsync(() => _receivedMails.Count >= 1);
        _receivedMails.Count.ShouldBe(1);
        _receivedMails[0].To.ToString().ShouldContain("has-email@test.de");
    }

    // ---------------------------------------------------------------
    // Helper functions
    // ---------------------------------------------------------------
    private async Task<UserEntity> CreateUser(UserRole role, string? email = null)
    {
        UserEntity user = new()
        {
            Id = Guid.NewGuid(),
            Username = Guid.NewGuid().ToString(),
            PasswordHash = Guid.NewGuid().ToString(),
            Role = role,
            Email = email != null ? _cryptoService.Encrypt(email) : null
        };

        await WithDbContext(async db =>
        {
            await db.Users.AddAsync(user);
            await db.SaveChangesAsync();
        });

        return user;
    }

    private static async Task<HttpResponseMessage> CreateReceiptAsync(HttpClient client)
    {
        using MultipartFormDataContent content = new()
        {
            { new StringContent("19.99"), "amount" },
            { new StringContent(DateTime.UtcNow.ToString("O")), "receiptDate" }
        };

        HttpResponseMessage response = await client.PostAsync("/receipts", content);
        response.EnsureSuccessStatusCode();

        return response;
    }

    private async Task SetMailSettingsAsync()
    {
        await WithDbContext(async db =>
        {
            await db.MailSettings.AddAsync(new MailSettingsEntity
            {
                SmtpServer = "localhost",
                Port = 2523,
                ImapServer = "localhost",
                ImapPort = 2523,
                Username = "test",
                Password = _cryptoService.Encrypt("test"),
                FromMail = "noreply@test.de",
                EnableSsl = false
            });
            await db.SaveChangesAsync();
        });
    }

    private async Task SetReceiptNotificationSettingAsync(UserRole role, bool enabled, string email)
    {
        await WithDbContext(async db =>
        {
            UserEntity user = new()
            {
                Id = Guid.NewGuid(),
                Username = Guid.NewGuid().ToString(),
                PasswordHash = Guid.NewGuid().ToString(),
                Role = role,
                Email = _cryptoService.Encrypt(email)
            };
            await db.Users.AddAsync(user);

            await db.UserSettings.AddAsync(new UserSettingEntity
            {
                UserId = user.Id,
                User = user,
                Type = UserSettingType.RECEIPT_NOTIFICATION,
                Enabled = enabled
            });
            await db.SaveChangesAsync();
        });
    }

    private ReceiptNotificationService BuildReceiptNotificationService(AppDbContext db)
    {
        Mock<IHubContext<EmailProgressHub>> mockHubContext = new();
        Mock<IHubClients> mockClients = new();
        Mock<ISingleClientProxy> mockClientProxy = new();
        mockClientProxy
            .Setup(p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        mockClients.Setup(c => c.All).Returns(mockClientProxy.Object);
        mockClients.Setup(c => c.Client(It.IsAny<string>())).Returns(mockClientProxy.Object);
        mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);

        MailService mailService = new(db, _cryptoService, mockHubContext.Object, new FakeImapClient());

        return new ReceiptNotificationService(db, mailService, _cryptoService);
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
