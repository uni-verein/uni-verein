using System;
using System.Threading.Tasks;
using UniVerein.Api.Services;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using UniVerein.IntegrationTests.Infrastructure;
using Shouldly;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class PendingSelfEnrollmentCleanupServiceTests : IntegrationTestBase
{
    private CryptoService _cryptoService = null!;

    public PendingSelfEnrollmentCleanupServiceTests(UniVereinWebApplicationFactory factory) : base(factory)
    {
    }

    public override async Task InitializeAsync()
    {
        _cryptoService = GetService<CryptoService>();
        Factory.FakeTime.SetUtcNow(DateTimeOffset.UtcNow);
        await WithDbContext(async db =>
        {
            db.PendingSelfEnrollments.RemoveRange(db.PendingSelfEnrollments.AsQueryable());
            await db.ForceSaveChangesAsync();
        });
    }

    public override async Task DisposeAsync()
    {
        await WithDbContext(async db =>
        {
            db.PendingSelfEnrollments.RemoveRange(db.PendingSelfEnrollments.AsQueryable());
            await db.ForceSaveChangesAsync();
        });
    }

    [Fact]
    public async Task CleanupExpiredAsync_ExpiredAwaitingConfirmationRow_IsHardDeleted()
    {
        // Arrange
        Guid expiredId = await CreatePendingSelfEnrollmentEntityAsync(PendingSelfEnrollmentStatus.AWAITING_EMAIL_CONFIRMATION,
            DateTimeOffset.UtcNow.AddHours(-1));
        PendingSelfEnrollmentService service = GetService<PendingSelfEnrollmentService>();

        // Act
        int removed = await service.CleanupExpiredAsync();

        // Assert
        removed.ShouldBe(1);
        await WithDbContext(async db =>
        {
            (await db.PendingSelfEnrollments.AnyAsync(x => x.Id == expiredId)).ShouldBeFalse();
        });
    }

    [Fact]
    public async Task CleanupExpiredAsync_NotYetExpiredAwaitingConfirmationRow_IsUntouched()
    {
        // Arrange
        Guid stillValidId = await CreatePendingSelfEnrollmentEntityAsync(PendingSelfEnrollmentStatus.AWAITING_EMAIL_CONFIRMATION,
            DateTimeOffset.UtcNow.AddHours(48));
        PendingSelfEnrollmentService service = GetService<PendingSelfEnrollmentService>();

        // Act
        int removed = await service.CleanupExpiredAsync();

        // Assert
        removed.ShouldBe(0);
        await WithDbContext(async db =>
        {
            (await db.PendingSelfEnrollments.AnyAsync(x => x.Id == stillValidId)).ShouldBeTrue();
        });
    }

    [Fact]
    public async Task CleanupExpiredAsync_ConfirmedPendingRowPastExpiry_IsNeverDeleted()
    {
        // Arrange
        Guid confirmedId = await CreatePendingSelfEnrollmentEntityAsync(PendingSelfEnrollmentStatus.PENDING,
            DateTimeOffset.UtcNow.AddHours(-1));
        PendingSelfEnrollmentService service = GetService<PendingSelfEnrollmentService>();

        // Act
        int removed = await service.CleanupExpiredAsync();

        // Assert
        removed.ShouldBe(0);
        await WithDbContext(async db =>
        {
            (await db.PendingSelfEnrollments.AnyAsync(x => x.Id == confirmedId)).ShouldBeTrue();
        });
    }

    private async Task<Guid> CreatePendingSelfEnrollmentEntityAsync(PendingSelfEnrollmentStatus status,
        DateTimeOffset tokenExpiresAt)
    {
        string email = $"{Guid.NewGuid()}@test.de";
        PendingSelfEnrollmentEntity pending = new()
        {
            Status = status,
            ConfirmationTokenHash = _cryptoService.Hash(Guid.NewGuid().ToString()),
            ConfirmationTokenExpiresAt = tokenExpiresAt,
            ConfirmedAt = status == PendingSelfEnrollmentStatus.PENDING ? DateTimeOffset.UtcNow : null,
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
            MemberCategoryId = await CreateMemberCategoryEntityAsync()
        };

        await WithDbContext(async db =>
        {
            await db.PendingSelfEnrollments.AddAsync(pending);
            await db.SaveChangesAsync();
        });

        return pending.Id;
    }

    private async Task<Guid> CreateMemberCategoryEntityAsync()
    {
        MemberCategoryEntity category = new() { Category = "TEST", Name = "Test category" };
        await WithDbContext(async db =>
        {
            await db.MemberCategories.AddAsync(category);
            await db.SaveChangesAsync();
        });

        return category.Id;
    }
}
