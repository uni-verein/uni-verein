using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UniVerein.Api.ApiRequests;
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
public class SelfEnrollmentNotificationServiceTests : IntegrationTestBase
{
    private SmtpServer.SmtpServer _smtpServer = null!;
    private CancellationTokenSource _cts = null!;
    private readonly List<MimeMessage> _receivedMails = new();
    private CryptoService _cryptoService = null!;
    private Guid _memberCategoryId;

    public SelfEnrollmentNotificationServiceTests(UniVereinWebApplicationFactory factory) : base(factory)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _cryptoService = GetService<CryptoService>();

        await WithDbContext(async db =>
        {
            db.UserSettings.RemoveRange(db.UserSettings.AsQueryable());
            db.Members.RemoveRange(db.Members.AsQueryable());
            db.PendingSelfEnrollments.RemoveRange(db.PendingSelfEnrollments.AsQueryable());
            db.WebPageConfigs.RemoveRange(db.WebPageConfigs.AsQueryable());
            await db.ForceSaveChangesAsync();

            MemberCategoryEntity category = new() { Category = "TEST", Name = "Test category" };
            await db.MemberCategories.AddAsync(category);
            await db.SaveChangesAsync();
            _memberCategoryId = category.Id;
        });

        ISmtpServerOptions options = new SmtpServerOptionsBuilder()
            .ServerName("localhost")
            .Port(2524)
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
            db.Members.RemoveRange(db.Members.AsQueryable());
            db.PendingSelfEnrollments.RemoveRange(db.PendingSelfEnrollments.AsQueryable());
            db.WebPageConfigs.RemoveRange(db.WebPageConfigs.AsQueryable());
            await db.ForceSaveChangesAsync();
        });
    }

    // ---------------------------------------------------------------
    // Full HTTP path: POST /self-enrollment -> confirmation mail -> POST /self-enrollment/confirm
    // -> notification mail to opted-in subscriber
    // ---------------------------------------------------------------

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    [InlineData(UserRole.ADMIN)]
    public async Task ConfirmSelfEnrollment_NotifiesOptedInSubscriber_RegardlessOfRole(UserRole subscriberRole)
    {
        // Arrange
        await SetMailSettingsAsync();
        await CreateSelfEnrollmentConfigEntity(true);
        await SetSelfEnrollmentNotificationSettingAsync(subscriberRole, enabled: true, email: "opted-in@test.de");
        HttpClient publicClient = CreateClient();

        // Act: submit -> confirmation mail -> confirm -> notification mail
        await publicClient.PostAsJsonAsync("/self-enrollment", CreateSelfEnrollmentRequest());
        await WaitForConditionAsync(() => _receivedMails.Count >= 1);
        string token = ExtractConfirmationToken(_receivedMails[0]);

        HttpResponseMessage confirmResponse = await publicClient.PostAsJsonAsync("/self-enrollment/confirm",
            new ConfirmSelfEnrollmentRequest { Token = token });

        // Assert
        confirmResponse.EnsureSuccessStatusCode();
        await WaitForConditionAsync(() => _receivedMails.Count >= 2);
        _receivedMails.Count.ShouldBe(2);
        _receivedMails[1].To.ToString().ShouldContain("opted-in@test.de");
    }

    [Fact]
    public async Task PostSelfEnrollment_SpoofedHostHeader_ConfirmationLinkUsesConfiguredPublicBaseUrl()
    {
        // Arrange
        await SetMailSettingsAsync();
        await CreateSelfEnrollmentConfigEntity(true);
        HttpClient publicClient = CreateClient();
        publicClient.DefaultRequestHeaders.Host = "evil.attacker.example";

        // Act
        await publicClient.PostAsJsonAsync("/self-enrollment", CreateSelfEnrollmentRequest());
        await WaitForConditionAsync(() => _receivedMails.Count >= 1);

        // Assert: the link must use the domain captured when self-enrollment was enabled
        // by an admin, never a Host header supplied by this anonymous, unauthenticated request.
        string body = _receivedMails[0].HtmlBody ?? _receivedMails[0].TextBody ?? string.Empty;
        body.ShouldContain("https://club.test.invalid/enroll/confirm");
        body.ShouldNotContain("evil.attacker.example");
    }

    [Fact]
    public async Task ConfirmSelfEnrollment_NoOneOptedIn_NoNotificationMail()
    {
        // Arrange
        await SetMailSettingsAsync();
        await CreateSelfEnrollmentConfigEntity(true);
        HttpClient publicClient = CreateClient();

        // Act
        await publicClient.PostAsJsonAsync("/self-enrollment", CreateSelfEnrollmentRequest());
        await WaitForConditionAsync(() => _receivedMails.Count >= 1);
        string token = ExtractConfirmationToken(_receivedMails[0]);

        HttpResponseMessage confirmResponse = await publicClient.PostAsJsonAsync("/self-enrollment/confirm",
            new ConfirmSelfEnrollmentRequest { Token = token });
        await Task.Delay(500);

        // Assert: only the confirmation mail was ever sent, no second (notification) mail follows.
        confirmResponse.EnsureSuccessStatusCode();
        _receivedMails.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ConfirmSelfEnrollment_UserWithoutSettingsRow_IsNotNotifiedByDefault()
    {
        // Arrange
        await SetMailSettingsAsync();
        await CreateSelfEnrollmentConfigEntity(true);
        await CreateUser(UserRole.ADMIN, "no-settings-row@test.de");
        HttpClient publicClient = CreateClient();

        // Act
        await publicClient.PostAsJsonAsync("/self-enrollment", CreateSelfEnrollmentRequest());
        await WaitForConditionAsync(() => _receivedMails.Count >= 1);
        string token = ExtractConfirmationToken(_receivedMails[0]);

        await publicClient.PostAsJsonAsync("/self-enrollment/confirm", new ConfirmSelfEnrollmentRequest { Token = token });
        await Task.Delay(500);

        // Assert
        _receivedMails.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ConfirmSelfEnrollment_SettingDisabled_NoNotificationMail()
    {
        // Arrange
        await SetMailSettingsAsync();
        await CreateSelfEnrollmentConfigEntity(true);
        await SetSelfEnrollmentNotificationSettingAsync(UserRole.USER, enabled: false, email: "opted-out@test.de");
        HttpClient publicClient = CreateClient();

        // Act
        await publicClient.PostAsJsonAsync("/self-enrollment", CreateSelfEnrollmentRequest());
        await WaitForConditionAsync(() => _receivedMails.Count >= 1);
        string token = ExtractConfirmationToken(_receivedMails[0]);

        await publicClient.PostAsJsonAsync("/self-enrollment/confirm", new ConfirmSelfEnrollmentRequest { Token = token });
        await Task.Delay(500);

        // Assert
        _receivedMails.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ConfirmSelfEnrollment_NoMailSettingsConfigured_ConfirmStillSucceeds()
    {
        // Arrange
        await CreateSelfEnrollmentConfigEntity(true);
        await SetSelfEnrollmentNotificationSettingAsync(UserRole.USER, enabled: true, email: "would-be-notified@test.de");
        (PendingSelfEnrollmentEntity pending, string token) = await CreatePendingSelfEnrollmentEntityDirectlyAsync();
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/self-enrollment/confirm",
            new ConfirmSelfEnrollmentRequest { Token = token });
        await Task.Delay(500);

        // Assert
        response.EnsureSuccessStatusCode();
        _receivedMails.ShouldBeEmpty();
        await WithDbContext(async db =>
        {
            PendingSelfEnrollmentEntity? confirmed =
                await db.PendingSelfEnrollments.FirstOrDefaultAsync(x => x.Id == pending.Id);
            confirmed.ShouldNotBeNull();
            confirmed!.Status.ShouldBe(PendingSelfEnrollmentStatus.PENDING);
        });
    }

    // ---------------------------------------------------------------
    // Direct service edge cases
    // ---------------------------------------------------------------

    [Fact]
    public async Task NotifyAsync_MultipleOptedInSubscribers_AllNotified()
    {
        // Arrange
        await SetMailSettingsAsync();
        await SetSelfEnrollmentNotificationSettingAsync(UserRole.USER, enabled: true, email: "sub1@test.de");
        await SetSelfEnrollmentNotificationSettingAsync(UserRole.ADMIN, enabled: true, email: "sub2@test.de");

        await WithDbContext(async db =>
        {
            PendingSelfEnrollmentEntity pending = BuildPendingEntity();
            await db.PendingSelfEnrollments.AddAsync(pending);
            await db.SaveChangesAsync();

            SelfEnrollmentNotificationService service = BuildNotificationService(db);

            // Act
            await service.NotifyAsync(pending);
        });

        // Assert
        await WaitForConditionAsync(() => _receivedMails.Count >= 2);
        _receivedMails.Count.ShouldBe(2);
    }

    [Fact]
    public async Task NotifyAsync_RecipientHasNoEmail_SkipsSilentlyAndNotifiesOthers()
    {
        // Arrange
        await SetMailSettingsAsync();

        UserEntity userWithoutEmail = await CreateUser(UserRole.USER);
        UserEntity userWithEmail = await CreateUser(UserRole.USER, "has-email@test.de");

        await WithDbContext(async db =>
        {
            db.Attach(userWithoutEmail);
            db.Attach(userWithEmail);

            await db.UserSettings.AddRangeAsync(
                new UserSettingEntity { UserId = userWithoutEmail.Id, User = userWithoutEmail, Type = UserSettingType.SELF_ENROLLMENT_NOTIFICATION, Enabled = true },
                new UserSettingEntity { UserId = userWithEmail.Id, User = userWithEmail, Type = UserSettingType.SELF_ENROLLMENT_NOTIFICATION, Enabled = true });
            await db.SaveChangesAsync();

            PendingSelfEnrollmentEntity pending = BuildPendingEntity();
            await db.PendingSelfEnrollments.AddAsync(pending);
            await db.SaveChangesAsync();

            SelfEnrollmentNotificationService service = BuildNotificationService(db);

            // Act
            await service.NotifyAsync(pending);
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

    private async Task SetMailSettingsAsync()
    {
        await WithDbContext(async db =>
        {
            await db.MailSettings.AddAsync(new MailSettingsEntity
            {
                SmtpServer = "localhost",
                Port = 2524,
                ImapServer = "localhost",
                ImapPort = 2524,
                Username = "test",
                Password = _cryptoService.Encrypt("test"),
                FromMail = "noreply@test.de",
                EnableSsl = false
            });
            await db.SaveChangesAsync();
        });
    }

    private async Task CreateSelfEnrollmentConfigEntity(bool enabled)
    {
        await WithDbContext(async db =>
        {
            await db.WebPageConfigs.AddAsync(new WebPageConfigEntity
            {
                SelfEnrollmentEnabled = enabled,
                PublicBaseUrl = enabled ? "https://club.test.invalid" : null
            });
            await db.SaveChangesAsync();
        });
    }

    private async Task SetSelfEnrollmentNotificationSettingAsync(UserRole role, bool enabled, string email)
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
                Type = UserSettingType.SELF_ENROLLMENT_NOTIFICATION,
                Enabled = enabled
            });
            await db.SaveChangesAsync();
        });
    }

    private SelfEnrollmentRequest CreateSelfEnrollmentRequest()
    {
        return new SelfEnrollmentRequest
        {
            Gender = Gender.MALE,
            FirstName = "Notify",
            LastName = Guid.NewGuid().ToString(),
            Birthday = DateTimeOffset.UtcNow.AddYears(-20),
            Street = "street",
            PostalCode = "24103",
            City = "Kiel",
            CountryCode = "DE",
            Email = $"{Guid.NewGuid()}@test.de",
            BulkMail = BulkMail.ALLOWED,
            StartOfStudies = DateTimeOffset.UtcNow,
            Motivation = "Studiere Informatik.",
            IBAN = Guid.NewGuid().ToString("N"),
            Bic = "DEUTDEDEXXX",
            EntryDate = DateTimeOffset.UtcNow
        };
    }

    private PendingSelfEnrollmentEntity BuildPendingEntity()
    {
        string email = $"{Guid.NewGuid()}@test.de";
        return new PendingSelfEnrollmentEntity
        {
            Status = PendingSelfEnrollmentStatus.PENDING,
            ConfirmationTokenHash = _cryptoService.Hash(Guid.NewGuid().ToString()),
            ConfirmationTokenExpiresAt = DateTimeOffset.UtcNow.AddHours(48),
            ConfirmedAt = DateTimeOffset.UtcNow,
            SubmittedIp = "127.0.0.1",
            Gender = Gender.MALE,
            FirstName = "Pending",
            LastName = "Applicant",
            BirthdayEncrypted = _cryptoService.Encrypt(DateTimeOffset.UtcNow.AddYears(-20)),
            StreetEncrypted = _cryptoService.Encrypt("street"),
            PostalCode = "24103",
            City = "Kiel",
            CountryCode = "DE",
            EmailEncrypted = _cryptoService.Encrypt(email),
            EmailHash = _cryptoService.Hash(email),
            PhoneEncrypted = _cryptoService.Encrypt("01512345678"),
            BulkMail = BulkMail.ALLOWED,
            StartOfStudies = DateTimeOffset.UtcNow,
            MotivationEncrypted = _cryptoService.Encrypt("Studiere Informatik."),
            MemberCategoryId = _memberCategoryId
        };
    }

    private async Task<(PendingSelfEnrollmentEntity Pending, string Token)> CreatePendingSelfEnrollmentEntityDirectlyAsync()
    {
        string token = "direct-token-" + Guid.NewGuid();
        PendingSelfEnrollmentEntity pending = new()
        {
            Status = PendingSelfEnrollmentStatus.AWAITING_EMAIL_CONFIRMATION,
            ConfirmationTokenHash = _cryptoService.Hash(token),
            ConfirmationTokenExpiresAt = DateTimeOffset.UtcNow.AddHours(48),
            SubmittedIp = "127.0.0.1",
            Gender = Gender.MALE,
            FirstName = "Pending",
            LastName = "Applicant",
            BirthdayEncrypted = _cryptoService.Encrypt(DateTimeOffset.UtcNow.AddYears(-20)),
            StreetEncrypted = _cryptoService.Encrypt("street"),
            PostalCode = "24103",
            City = "Kiel",
            CountryCode = "DE",
            EmailEncrypted = _cryptoService.Encrypt($"{Guid.NewGuid()}@test.de"),
            EmailHash = _cryptoService.Hash($"{Guid.NewGuid()}@test.de"),
            PhoneEncrypted = _cryptoService.Encrypt("01512345678"),
            BulkMail = BulkMail.ALLOWED,
            StartOfStudies = DateTimeOffset.UtcNow,
            MotivationEncrypted = _cryptoService.Encrypt("Studiere Informatik."),
            MemberCategoryId = _memberCategoryId
        };

        await WithDbContext(async db =>
        {
            await db.PendingSelfEnrollments.AddAsync(pending);
            await db.SaveChangesAsync();
        });

        return (pending, token);
    }

    private SelfEnrollmentNotificationService BuildNotificationService(AppDbContext db)
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

        return new SelfEnrollmentNotificationService(db, mailService, _cryptoService);
    }

    private static string ExtractConfirmationToken(MimeMessage mail)
    {
        string body = mail.HtmlBody ?? mail.TextBody ?? string.Empty;
        System.Text.RegularExpressions.Match match = Regex.Match(body, "token=([A-Za-z0-9_-]+)");
        match.Success.ShouldBeTrue("Confirmation mail did not contain a token link.");
        return match.Groups[1].Value;
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
