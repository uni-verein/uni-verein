using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UniVerein.Api.Models;
using UniVerein.Api.Models.Enums;
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
public class PendingSelfEnrollmentApprovalMailTests : IntegrationTestBase
{
    private SmtpServer.SmtpServer _smtpServer = null!;
    private CancellationTokenSource _cts = null!;
    private readonly List<MimeMessage> _receivedMails = new();
    private CryptoService _cryptoService = null!;
    private Guid _memberCategoryId;

    public PendingSelfEnrollmentApprovalMailTests(UniVereinWebApplicationFactory factory) : base(factory)
    {
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        _cryptoService = GetService<CryptoService>();

        await WithDbContext(async db =>
        {
            db.Members.RemoveRange(db.Members.AsQueryable());
            db.PendingSelfEnrollments.RemoveRange(db.PendingSelfEnrollments.AsQueryable());
            await db.ForceSaveChangesAsync();

            MemberCategoryEntity category = new() { Category = "TEST", Name = "Test category" };
            await db.MemberCategories.AddAsync(category);
            await db.SaveChangesAsync();
            _memberCategoryId = category.Id;
        });

        ISmtpServerOptions options = new SmtpServerOptionsBuilder()
            .ServerName("localhost")
            .Port(2526)
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
            db.Members.RemoveRange(db.Members.AsQueryable());
            db.PendingSelfEnrollments.RemoveRange(db.PendingSelfEnrollments.AsQueryable());
            await db.ForceSaveChangesAsync();
        });
    }

    [Fact]
    public async Task ApproveAsync_HappyPath_SendsAcceptanceMailToApplicant()
    {
        // Arrange
        await SetMailSettingsAsync();
        string applicantEmail = $"{Guid.NewGuid()}@test.de";
        Guid pendingId = await CreatePendingSelfEnrollmentAsync(applicantEmail);

        // Act
        PendingSelfEnrollmentService service = await BuildServiceAsync();
        await service.ApproveAsync(pendingId);

        // Assert
        await WaitForConditionAsync(() => _receivedMails.Count >= 1);
        _receivedMails.Count.ShouldBe(1);
        _receivedMails[0].To.ToString().ShouldContain(applicantEmail);
        (_receivedMails[0].Subject ?? string.Empty).ShouldContain("angenommen");
    }

    [Fact]
    public async Task ApproveAsync_NoMailSettingsConfigured_ApprovalStillSucceeds()
    {
        // Arrange
        Guid pendingId = await CreatePendingSelfEnrollmentAsync($"{Guid.NewGuid()}@test.de");

        // Act
        PendingSelfEnrollmentService service = await BuildServiceAsync();
        PendingSelfEnrollmentApprovalResult result = await service.ApproveAsync(pendingId);
        await Task.Delay(500);

        // Assert
        result.Status.ShouldBe(PendingSelfEnrollmentActionStatus.SUCCESS);
        result.Member.ShouldNotBeNull();
        _receivedMails.ShouldBeEmpty();
        await WithDbContext(async db =>
        {
            PendingSelfEnrollmentEntity? stillThere = await db.PendingSelfEnrollments
                .FirstOrDefaultAsync(x => x.Id == pendingId);
            stillThere.ShouldBeNull();
        });
    }

    [Fact]
    public async Task RejectAsync_NoAcceptanceMailSent()
    {
        // Arrange
        await SetMailSettingsAsync();
        Guid pendingId = await CreatePendingSelfEnrollmentAsync($"{Guid.NewGuid()}@test.de");

        // Act
        PendingSelfEnrollmentService service = await BuildServiceAsync();
        await service.RejectAsync(pendingId, "not a fit");
        await Task.Delay(500);

        // Assert
        _receivedMails.ShouldBeEmpty();
    }

    // ---------------------------------------------------------------
    // Helper functions
    // ---------------------------------------------------------------

    private async Task SetMailSettingsAsync()
    {
        await WithDbContext(async db =>
        {
            await db.MailSettings.AddAsync(new MailSettingsEntity
            {
                SmtpServer = "localhost",
                Port = 2526,
                ImapServer = "localhost",
                ImapPort = 2526,
                Username = "test",
                Password = _cryptoService.Encrypt("test"),
                FromMail = "noreply@test.de",
                EnableSsl = false
            });
            await db.SaveChangesAsync();
        });
    }

    private async Task<Guid> CreatePendingSelfEnrollmentAsync(string email)
    {
        Guid id = Guid.Empty;
        await WithDbContext(async db =>
        {
            PendingSelfEnrollmentEntity pending = new()
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
                MotivationEncrypted = _cryptoService.Encrypt("Studiere Informatik."),
                BulkMail = BulkMail.ALLOWED,
                StartOfStudies = DateTimeOffset.UtcNow,
                MemberCategoryId = _memberCategoryId
            };
            await db.PendingSelfEnrollments.AddAsync(pending);
            await db.SaveChangesAsync();
            id = pending.Id;
        });

        return id;
    }

    private async Task<PendingSelfEnrollmentService> BuildServiceAsync()
    {
        AppDbContext db = GetService<AppDbContext>();

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
        MemberService memberService = GetService<MemberService>();
        SelfEnrollmentNotificationService notificationService =
            new(db, mailService, _cryptoService);
        AuditService auditService = GetService<AuditService>();
        TimeProvider timeProvider = GetService<TimeProvider>();

        return new PendingSelfEnrollmentService(db, _cryptoService, memberService, mailService, notificationService,
            auditService, timeProvider);
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
