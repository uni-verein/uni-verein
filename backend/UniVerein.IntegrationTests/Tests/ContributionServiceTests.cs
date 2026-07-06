using UniVerein.Api.Services;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using UniVerein.IntegrationTests.Infrastructure;
using Shouldly;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class ContributionServiceTests : IntegrationTestBase
{
    public ContributionServiceTests(UniVereinWebApplicationFactory factory) : base(factory)
    {
    }

    public override async Task InitializeAsync()
    {
        await WithDbContext(async db =>
        {
            db.ContributionPlans.RemoveRange(db.ContributionPlans.AsQueryable());
            db.Contributions.RemoveRange(db.Contributions.AsQueryable());
            db.SepaExports.RemoveRange(db.SepaExports.AsQueryable());
            db.Members.RemoveRange(db.Members.AsQueryable());
            await db.ForceSaveChangesAsync();
        });
    }

    public override async Task DisposeAsync()
    {
        await WithDbContext(async db =>
        {
            db.ContributionPlans.RemoveRange(db.ContributionPlans.AsQueryable());
            db.Contributions.RemoveRange(db.Contributions.AsQueryable());
            db.SepaExports.RemoveRange(db.SepaExports.AsQueryable());
            db.Members.RemoveRange(db.Members.AsQueryable());
            await db.ForceSaveChangesAsync();
        });
    }

    [Fact]
    public async Task DeletePaidContributions_SuccessAsync()
    {
        // Arrange
        await CreateContributionEntityAsync(paid: DateTimeOffset.UtcNow.AddDays(-14).AddSeconds(-1));
        await CreateContributionEntityAsync(paid: DateTimeOffset.UtcNow.AddDays(-14).AddSeconds(-1));
        ContributionService contributionService = GetService<ContributionService>();

        // Act
        await contributionService.DeletePaidContributions();

        // Arrange
        await WithDbContext(async db =>
        {
            (await db.Contributions.Where(x => x.DeletedAt == null).CountAsync()).ShouldBe(0);
        });
    }

    [Fact]
    public async Task DeleteNoPaidContributionsWhereMarkedPaidOnTheLast14Days_SuccessAsync()
    {
        // Arrange
        await CreateContributionEntityAsync(paid: DateTimeOffset.UtcNow.AddDays(-14).AddSeconds(1));
        await CreateContributionEntityAsync(paid: DateTimeOffset.UtcNow.AddDays(-14).AddSeconds(1));
        ContributionService contributionService = GetService<ContributionService>();

        // Act
        await contributionService.DeletePaidContributions();

        // Arrange
        await WithDbContext(async db =>
        {
            (await db.Contributions.Where(x => x.DeletedAt == null).CountAsync()).ShouldBe(2);
        });
    }

    [Fact]
    public async Task GenerateDueContributions_NotFirstOfMonth_DoesNothing()
    {
        // Arrange
        Factory.FakeTime.SetUtcNow(new DateTimeOffset(2026, 5, 15, 0, 0, 0, TimeSpan.Zero));
        ContributionService contributionService = new ContributionService(GetService<AppDbContext>(), Factory.FakeTime);

        // Act
        await contributionService.GenerateDueContributions();

        // Arrange
        await WithDbContext(async db =>
        {
            Assert.Empty(await db.Contributions.ToListAsync());
            Assert.Empty(await db.SepaExports.ToListAsync());
        });
    }

    [Fact]
    public async Task GenerateDueContributions_NoMembers_DoesNothing()
    {
        // Arrange
        Factory.FakeTime.SetUtcNow(new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero));
        ContributionService contributionService = new ContributionService(GetService<AppDbContext>(), Factory.FakeTime);

        // Act
        await contributionService.GenerateDueContributions();

        // Arrange
        await WithDbContext(async db =>
        {
            Assert.Empty(await db.Contributions.Where(x => x.DeletedAt == null).ToListAsync());
            Assert.Empty(await db.SepaExports.Where(x => x.DeletedAt == null).ToListAsync());
        });
    }

    [Fact]
    public async Task GenerateDueContributions_ExportAlreadyCreatedToday_DoesNothing()
    {
        // Arrange
        var today = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        Factory.FakeTime.SetUtcNow(today);
        ContributionService contributionService = new ContributionService(GetService<AppDbContext>(), Factory.FakeTime);
        await CreateMemberEntityAsync(Interval.MONTHLY);
        SepaExportEntity sepaExportEntity = await CreateSepaExportEntityAsync(today);

        // Act
        await contributionService.GenerateDueContributions();

        // Assert
        await WithDbContext(async db =>
        {
            List<SepaExportEntity> sepaExport = await db.SepaExports.Where(x => x.DeletedAt == null).ToListAsync();
            Assert.Single(sepaExport);
            sepaExport.First().Id.ShouldBe(sepaExportEntity.Id);
        });
    }

    [Fact]
    public async Task GenerateDueContributions_MonthlyContribution_CreatesContributionAndExport()
    {
        // Arrange
        var today = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        Factory.FakeTime.SetUtcNow(today);
        ContributionService contributionService = new ContributionService(GetService<AppDbContext>(), Factory.FakeTime);
        MemberEntity memberEntity = await CreateMemberEntityAsync(Interval.MONTHLY);

        // Act
        await contributionService.GenerateDueContributions();

        // Assert
        await WithDbContext(async db =>
        {
            List<ContributionEntity> contributions =
                await db.Contributions.Where(x => x.DeletedAt == null).ToListAsync();
            Assert.Single(contributions);
            Assert.Equal(memberEntity.Id, contributions[0].MemberId);
            Assert.Equal(memberEntity.ContributionPlan!.Amount, contributions[0].Amount);
            Assert.Equal(new DateTime(2026, 3, 1), contributions[0].DueDate);

            List<SepaExportEntity> exports = await db.SepaExports.ToListAsync();
            Assert.Single(exports);
            Assert.Equal(contributions[0].Amount, exports[0].Amount);
            Assert.Equal(1, exports[0].Count);
        });
    }

    [Theory]
    [InlineData(2026, 2, 1)]
    [InlineData(2026, 6, 1)]
    [InlineData(2026, 11, 1)]
    public async Task GenerateDueContributions_YearlyContribution_SkipsNonJanuaryMonths(int year, int month, int day)
    {
        // Arrange
        var today = new DateTimeOffset(year, month, day, 0, 0, 0, TimeSpan.Zero);
        Factory.FakeTime.SetUtcNow(today);
        ContributionService contributionService = new ContributionService(GetService<AppDbContext>(), Factory.FakeTime);
        await CreateMemberEntityAsync(Interval.YEARLY);

        // Act
        await contributionService.GenerateDueContributions();

        // Assert
        await WithDbContext(async db =>
        {
            Assert.Empty(await db.Contributions.ToListAsync());
            Assert.Empty(await db.SepaExports.ToListAsync());
        });
    }

    [Fact]
    public async Task GenerateDueContributions_YearlyContribution_ProcessedInJanuary()
    {
        // Arrange
        var january = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        Factory.FakeTime.SetUtcNow(january);
        ContributionService contributionService = new ContributionService(GetService<AppDbContext>(), Factory.FakeTime);
        MemberEntity memberEntity = await CreateMemberEntityAsync(Interval.YEARLY);

        // Act
        await contributionService.GenerateDueContributions();

        // Assert
        await WithDbContext(async db =>
        {
            Assert.Single(await db.Contributions.ToListAsync());
            Assert.Single(await db.SepaExports.ToListAsync());
            (await db.Contributions.FirstOrDefaultAsync(x => x.DeletedAt == null && x.MemberId == memberEntity.Id))
                .ShouldNotBeNull();
        });
    }

    [Fact]
    public async Task GenerateDueContributions_MixedIntervals_OnlyProcessesCorrectOnes()
    {
        // Arrange
        var march = new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero);
        Factory.FakeTime.SetUtcNow(march);
        ContributionService contributionService = new ContributionService(GetService<AppDbContext>(), Factory.FakeTime);
        MemberEntity memberEntity = await CreateMemberEntityAsync(Interval.MONTHLY);
        await CreateMemberEntityAsync(Interval.YEARLY);

        // Act
        await contributionService.GenerateDueContributions();

        // Assert
        await WithDbContext(async db =>
        {
            List<ContributionEntity> contributions =
                await db.Contributions.Where(x => x.DeletedAt == null).ToListAsync();
            Assert.Single(contributions);
            Assert.Equal(memberEntity.ContributionPlan!.Amount, contributions[0].Amount);

            SepaExportEntity export = await db.SepaExports.Where(x => x.DeletedAt == null).SingleAsync();
            Assert.Equal(memberEntity.ContributionPlan!.Amount, export.Amount);
            Assert.Equal(1, export.Count);
        });
    }

    // ---------------------------------------------------------------
    // Helper functions
    // ---------------------------------------------------------------

    private async Task<(MemberEntity, ContributionEntity)> CreateContributionEntityAsync(string? firstName = null,
        string? lastName = null, DateTimeOffset? paid = null)
    {
        Guid memberId = Guid.NewGuid();
        MemberEntity memberEntity = new()
        {
            MandateId = memberId.ToString(),
            Id = memberId,
            FirstName = firstName ?? Guid.NewGuid().ToString(),
            LastName = lastName ?? Guid.NewGuid().ToString(),
            BirthdayEncrypted =  string.Empty,
            City = "City",
            StreetEncrypted =  string.Empty,
            PostalCode = "12345",
            CountryCode = "DE"
        };

        ContributionEntity contributionEntity = new()
        {
            Id = Guid.NewGuid(),
            MemberId = memberEntity.Id,
            MemberEntity = memberEntity,
            Amount = Random.Shared.Next(12, 1000),
            DueDate = DateTime.Now,
            Paid = paid
        };

        AppDbContext db = GetService<AppDbContext>();
        await db.Members.AddAsync(memberEntity);
        await db.Contributions.AddAsync(contributionEntity);
        await db.SaveChangesAsync();

        return (memberEntity, contributionEntity);
    }

    private async Task<SepaExportEntity> CreateSepaExportEntityAsync(DateTimeOffset? createdAt = null)
    {
        SepaExportEntity sepaExportEntity = new()
        {
            Id = Guid.NewGuid(),
            Name = string.Empty,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
            Count = 1,
            Amount = Random.Shared.Next(12, 1000),
        };

        AppDbContext db = GetService<AppDbContext>();
        await db.SepaExports.AddAsync(sepaExportEntity);
        await db.SaveChangesAsync();

        return sepaExportEntity;
    }

    private async Task<MemberEntity> CreateMemberEntityAsync(Interval? interval = null)
    {
        ContributionPlanEntity contributionPlanEntity = new()
        {
            Id = Guid.NewGuid(),
            Amount = Random.Shared.Next(12, 1000),
            Interval = interval ?? Interval.MONTHLY
        };

        Guid memberId = Guid.NewGuid();
        MemberEntity memberEntity = new()
        {
            MandateId = memberId.ToString(),
            Id = memberId,
            FirstName = Guid.NewGuid().ToString(),
            LastName = Guid.NewGuid().ToString(),
            BirthdayEncrypted =  string.Empty,
            City = string.Empty,
            CountryCode = "DE",
            StreetEncrypted =  string.Empty,
            PostalCode = "12345",
            ContributionPlan = contributionPlanEntity,
            ContributionPlanId = contributionPlanEntity.Id
        };

        AppDbContext db = GetService<AppDbContext>();
        await db.ContributionPlans.AddAsync(contributionPlanEntity);
        await db.Members.AddAsync(memberEntity);
        await db.SaveChangesAsync();

        return memberEntity;
    }
}