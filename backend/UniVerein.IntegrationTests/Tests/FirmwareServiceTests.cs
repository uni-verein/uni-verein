using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using UniVerein.Api.Models.Firmware;
using UniVerein.Api.Services;
using UniVerein.Api.Services.Firmware;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using UniVerein.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MimeKit;
using Moq;
using Shouldly;
using SmtpServer;
using SmtpServer.Storage;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

[Collection("NonParallelTests")]
public class FirmwareServiceTests : IntegrationTestBase
{
    private SmtpServer.SmtpServer _smtpServer = null!;
    private CancellationTokenSource _cts = null!;
    private readonly List<MimeMessage> _receivedMails = new();
    private CryptoService _cryptoService = null!;

    public FirmwareServiceTests(UniVereinWebApplicationFactory factory) : base(factory)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _cryptoService = GetService<CryptoService>();

        await WithDbContext(async db =>
        {
            db.FirmwareVersionNotifications.RemoveRange(db.FirmwareVersionNotifications.AsQueryable());
            db.FirmwareVersions.RemoveRange(db.FirmwareVersions.AsQueryable());
            await db.ForceSaveChangesAsync();
        });

        var options = new SmtpServerOptionsBuilder()
            .ServerName("localhost")
            .Port(2522)
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

    public override async Task DisposeAsync()
    {
        _cts.Cancel();
        await WithDbContext(async db =>
        {
            db.FirmwareVersionNotifications.RemoveRange(db.FirmwareVersionNotifications.AsQueryable());
            db.FirmwareVersions.RemoveRange(db.FirmwareVersions.AsQueryable());
            await db.ForceSaveChangesAsync();
        });
    }

    [Fact]
    public async Task CheckLatestFirmwareAsync_NoVersionConfigured_DoesNotCallGithub()
    {
        // Arrange
        (HttpClient httpClient, FakeHttpMessageHandler handler) = BuildHttpClient(BuildRelease("1.0.0"));
        IConfiguration configuration = BuildConfiguration(null);

        await WithDbContext(async db =>
        {
            FirmwareService service = BuildService(httpClient, configuration, db);

            // Act
            await service.CheckLatestFirmwareAsync(CancellationToken.None);
        });

        // Assert
        handler.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task CheckLatestFirmwareAsync_GithubReturnsNoRelease_DoesNotCreateFirmwareVersion()
    {
        // Arrange
        (HttpClient httpClient, _) = BuildHttpClient<GithubReleaseResponse>(null);
        IConfiguration configuration = BuildConfiguration("1.0.0");

        await WithDbContext(async db =>
        {
            FirmwareService service = BuildService(httpClient, configuration, db);

            // Act
            await service.CheckLatestFirmwareAsync(CancellationToken.None);
        });

        // Assert
        await WithDbContext(async db =>
        {
            (await db.FirmwareVersions.CountAsync()).ShouldBe(0);
        });
    }

    [Fact]
    public async Task CheckLatestFirmwareAsync_CurrentVersionUnparsable_DoesNotCreateFirmwareVersion()
    {
        // Arrange
        (HttpClient httpClient, _) = BuildHttpClient(BuildRelease("1.2.0"));
        IConfiguration configuration = BuildConfiguration("not-a-version");

        await WithDbContext(async db =>
        {
            FirmwareService service = BuildService(httpClient, configuration, db);

            // Act
            await service.CheckLatestFirmwareAsync(CancellationToken.None);
        });

        // Assert
        await WithDbContext(async db =>
        {
            (await db.FirmwareVersions.CountAsync()).ShouldBe(0);
        });
    }

    [Fact]
    public async Task CheckLatestFirmwareAsync_GithubVersionUnparsable_DoesNotCreateFirmwareVersion()
    {
        // Arrange
        (HttpClient httpClient, _) = BuildHttpClient(BuildRelease("not-a-version"));
        IConfiguration configuration = BuildConfiguration("1.0.0");

        await WithDbContext(async db =>
        {
            FirmwareService service = BuildService(httpClient, configuration, db);

            // Act
            await service.CheckLatestFirmwareAsync(CancellationToken.None);
        });

        // Assert
        await WithDbContext(async db =>
        {
            (await db.FirmwareVersions.CountAsync()).ShouldBe(0);
        });
    }

    [Theory]
    [InlineData("1.0.0")]
    [InlineData("0.9.0")]
    public async Task CheckLatestFirmwareAsync_LatestVersionNotNewerThanCurrent_DoesNotCreateFirmwareVersion(
        string latestVersion)
    {
        // Arrange
        (HttpClient httpClient, _) = BuildHttpClient(BuildRelease(latestVersion));
        IConfiguration configuration = BuildConfiguration("1.0.0");

        await WithDbContext(async db =>
        {
            FirmwareService service = BuildService(httpClient, configuration, db);

            // Act
            await service.CheckLatestFirmwareAsync(CancellationToken.None);
        });

        // Assert
        await WithDbContext(async db =>
        {
            (await db.FirmwareVersions.CountAsync()).ShouldBe(0);
        });
    }

    [Fact]
    public async Task CheckLatestFirmwareAsync_NewerVersionAvailable_CreatesFirmwareVersionEntity()
    {
        // Arrange
        DateTimeOffset publishedAt = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);
        (HttpClient httpClient, _) = BuildHttpClient(
            BuildRelease("v1.6.0", tagName: "v1.6.0", body: "Release notes", publishedAt: publishedAt));
        IConfiguration configuration = BuildConfiguration("1.0.0");

        await WithDbContext(async db =>
        {
            FirmwareService service = BuildService(httpClient, configuration, db);

            // Act
            await service.CheckLatestFirmwareAsync(CancellationToken.None);
        });

        // Assert
        await WithDbContext(async db =>
        {
            FirmwareVersionEntity? entity = await db.FirmwareVersions.FirstOrDefaultAsync();
            entity.ShouldNotBeNull();
            entity.Version.ShouldBe("v1.6.0");
            entity.TagName.ShouldBe("v1.6.0");
            entity.ReleaseNotes.ShouldBe("Release notes");
            entity.PublishedAt.ShouldBe(publishedAt);
        });
    }

    [Fact]
    public async Task CheckLatestFirmwareAsync_NewerVersionButNoMailSettings_DoesNotSendNotifications()
    {
        // Arrange
        (HttpClient httpClient, _) = BuildHttpClient(BuildRelease("1.5.0"));
        IConfiguration configuration = BuildConfiguration("1.0.0");
        await SetSeedAdminEmailAsync();

        await WithDbContext(async db =>
        {
            FirmwareService service = BuildService(httpClient, configuration, db);

            // Act
            await service.CheckLatestFirmwareAsync(CancellationToken.None);
        });

        // Assert
        await WithDbContext(async db =>
        {
            (await db.FirmwareVersions.CountAsync()).ShouldBe(1);
            (await db.FirmwareVersionNotifications.CountAsync()).ShouldBe(0);
        });
        _receivedMails.ShouldBeEmpty();
    }

    [Fact]
    public async Task CheckLatestFirmwareAsync_CalledTwiceForSameRelease_DoesNotDuplicateFirmwareVersionEntity()
    {
        // Arrange
        IConfiguration configuration = BuildConfiguration("1.0.0");

        await WithDbContext(async db =>
        {
            (HttpClient httpClient1, _) = BuildHttpClient(BuildRelease("1.3.0"));
            await BuildService(httpClient1, configuration, db).CheckLatestFirmwareAsync(CancellationToken.None);
        });
        await WithDbContext(async db =>
        {
            (HttpClient httpClient2, _) = BuildHttpClient(BuildRelease("1.3.0"));
            await BuildService(httpClient2, configuration, db).CheckLatestFirmwareAsync(CancellationToken.None);
        });

        // Assert
        await WithDbContext(async db =>
        {
            (await db.FirmwareVersions.CountAsync(f => f.Version == "1.3.0")).ShouldBe(1);
        });
    }

    [Fact]
    public async Task CheckLatestFirmwareAsync_NotifiesAdminsAndSkipsAlreadyNotifiedAndNonAdmins()
    {
        // Arrange
        Guid seedAdminId = await SetSeedAdminEmailAsync();
        (HttpClient httpClient, _) = BuildHttpClient(BuildRelease("2.0.0"));
        IConfiguration configuration = BuildConfiguration("1.0.0");

        Guid alreadyNotifiedAdminId = Guid.NewGuid();
        Guid pendingAdminId = Guid.NewGuid();
        Guid nonAdminId = Guid.NewGuid();
        Guid firmwareVersionId = Guid.NewGuid();

        await WithDbContext(async db =>
        {
            FirmwareVersionEntity existingFirmwareVersion = new()
            {
                Id = firmwareVersionId,
                Version = "2.0.0",
                TagName = "2.0.0",
                PublishedAt = DateTimeOffset.UtcNow
            };
            await db.FirmwareVersions.AddAsync(existingFirmwareVersion);

            UserEntity alreadyNotifiedAdmin = new()
            {
                Id = alreadyNotifiedAdminId,
                Username = Guid.NewGuid().ToString(),
                PasswordHash = Guid.NewGuid().ToString(),
                Role = UserRole.ADMIN,
                Email = _cryptoService.Encrypt("already-notified@test.de")
            };
            UserEntity pendingAdmin = new()
            {
                Id = pendingAdminId,
                Username = Guid.NewGuid().ToString(),
                PasswordHash = Guid.NewGuid().ToString(),
                Role = UserRole.ADMIN,
                Email = _cryptoService.Encrypt("pending-admin@test.de")
            };
            UserEntity nonAdmin = new()
            {
                Id = nonAdminId,
                Username = Guid.NewGuid().ToString(),
                PasswordHash = Guid.NewGuid().ToString(),
                Role = UserRole.USER,
                Email = _cryptoService.Encrypt("non-admin@test.de")
            };
            await db.Users.AddRangeAsync(alreadyNotifiedAdmin, pendingAdmin, nonAdmin);
            await db.SaveChangesAsync();

            UserEntity seedAdmin = await db.Users.SingleAsync(u => u.Id == seedAdminId);
            await db.FirmwareVersionNotifications.AddRangeAsync(
                new FirmwareVersionNotificationEntity()
                {
                    FirmwareVersionId = firmwareVersionId,
                    FirmwareVersion = existingFirmwareVersion,
                    UserId = alreadyNotifiedAdminId,
                    User = alreadyNotifiedAdmin
                },
                new FirmwareVersionNotificationEntity()
                {
                    FirmwareVersionId = firmwareVersionId,
                    FirmwareVersion = existingFirmwareVersion,
                    UserId = seedAdminId,
                    User = seedAdmin
                });
            await db.SaveChangesAsync();
        });

        await WithDbContext(async db =>
        {
            await db.MailSettings.AddAsync(new MailSettingsEntity()
            {
                SmtpServer = "localhost",
                Port = 2522,
                ImapServer = "localhost",
                ImapPort = 2522,
                Username = "test",
                Password = _cryptoService.Encrypt("test"),
                FromMail = "noreply@test.de",
                EnableSsl = false
            });
            await db.SaveChangesAsync();

            FirmwareService service = BuildService(httpClient, configuration, db);

            // Act
            await service.CheckLatestFirmwareAsync(CancellationToken.None);
        });

        // Assert
        await WaitForConditionAsync(() => _receivedMails.Count >= 1, timeoutMs: 5000);

        _receivedMails.Count.ShouldBe(1);
        _receivedMails[0].To.ToString().ShouldContain("pending-admin@test.de");

        await WithDbContext(async db =>
        {
            List<Guid> notifiedUserIds = await db.FirmwareVersionNotifications
                .Where(n => n.FirmwareVersionId == firmwareVersionId)
                .Select(n => n.UserId)
                .ToListAsync();

            notifiedUserIds.Count.ShouldBe(3);
            notifiedUserIds.ShouldContain(alreadyNotifiedAdminId);
            notifiedUserIds.ShouldContain(seedAdminId);
            notifiedUserIds.ShouldContain(pendingAdminId);
            notifiedUserIds.ShouldNotContain(nonAdminId);
        });
    }

    // ---------------------------------------------------------------
    // Helper functions
    // ---------------------------------------------------------------

    private static IConfiguration BuildConfiguration(string? version)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Version"] = version })
            .Build();
    }

    private static GithubReleaseResponse BuildRelease(string name, string? tagName = null,
        string body = "Release notes", DateTimeOffset? publishedAt = null)
    {
        return new GithubReleaseResponse
        {
            Name = name,
            TagName = tagName ?? name,
            Body = body,
            PublishedAt = publishedAt ?? DateTimeOffset.UtcNow
        };
    }

    private FirmwareService BuildService(HttpClient httpClient, IConfiguration configuration, AppDbContext db)
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
        return new FirmwareService(httpClient, configuration, db, mailService, _cryptoService);
    }
    
    private async Task<Guid> SetSeedAdminEmailAsync()
    {
        Guid seedAdminId = Guid.Empty;
        await WithDbContext(async db =>
        {
            UserEntity seedAdmin = await db.Users.SingleAsync(u => u.Username == "admin");
            seedAdmin.Email = _cryptoService.Encrypt("seed-admin@test.de");
            await db.SaveChangesAsync();
            seedAdminId = seedAdmin.Id;
        });
        return seedAdminId;
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
