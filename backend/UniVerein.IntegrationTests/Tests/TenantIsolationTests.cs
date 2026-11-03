using System.IdentityModel.Tokens.Jwt;
using System.Net;
using UniVerein.Api.Services;
using UniVerein.Api.Tenancy;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using UniVerein.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class TenantIsolationTests : IntegrationTestBase
{
    public TenantIsolationTests(UniVereinWebApplicationFactory factory)
        : base(factory)
    {
    }

    private async Task<Guid> CreateTenantAsync()
    {
        Guid tenantId = Guid.NewGuid();
        await WithDbContext(async db =>
        {
            db.Tenants.Add(new TenantEntity { Id = tenantId, Name = $"Tenant {tenantId}" });
            await db.SaveChangesAsync();
        });
        return tenantId;
    }

    private async Task WithTenantContext(Guid tenantId, Func<AppDbContext, Task> action)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().TenantId = tenantId;
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    [Fact]
    public void EveryTenantScopedEntity_HasTheTenantFilter()
    {
        // Arrange
        AppDbContext db = GetService<AppDbContext>();

        // Act
        List<string> withoutFilter = db.Model.GetEntityTypes()
            .Where(e => typeof(TenantScopedEntity).IsAssignableFrom(e.ClrType))
            .Where(e => e.GetDeclaredQueryFilters().All(f => f.Key != nameof(QueryFilter.TENANT)))
            .Select(e => e.ClrType.Name)
            .ToList();

        // Assert
        withoutFilter.ShouldBeEmpty();
    }

    [Fact]
    public async Task NewEntity_GetsTheTenantOfTheContext()
    {
        // Arrange
        Guid otherTenant = await CreateTenantAsync();
        Guid defaultCategoryId = Guid.NewGuid();
        Guid otherCategoryId = Guid.NewGuid();

        // Act
        await WithDbContext(async db =>
        {
            db.MemberCategories.Add(new MemberCategoryEntity { Id = defaultCategoryId, Category = "A", Name = "A" });
            await db.SaveChangesAsync();
        });
        await WithTenantContext(otherTenant, async db =>
        {
            db.MemberCategories.Add(new MemberCategoryEntity { Id = otherCategoryId, Category = "B", Name = "B" });
            await db.SaveChangesAsync();
        });

        // Assert
        await WithDbContext(async db =>
        {
            List<MemberCategoryEntity> all = await db.MemberCategories.IgnoreQueryFilters()
                .Where(x => x.Id == defaultCategoryId || x.Id == otherCategoryId).ToListAsync();
            all.Single(x => x.Id == defaultCategoryId).TenantId.ShouldBe(TenantEntity.DefaultId);
            all.Single(x => x.Id == otherCategoryId).TenantId.ShouldBe(otherTenant);
        });
    }

    [Fact]
    public async Task SyncSaveChangesAndForceSave_StampTheTenant()
    {
        // Arrange
        Guid otherTenant = await CreateTenantAsync();
        Guid syncId = Guid.NewGuid();
        Guid forceId = Guid.NewGuid();

        // Act
        await WithTenantContext(otherTenant, async db =>
        {
            db.MemberCategories.Add(new MemberCategoryEntity { Id = syncId, Category = "S", Name = "S" });
            db.SaveChanges();
            db.MemberCategories.Add(new MemberCategoryEntity { Id = forceId, Category = "F", Name = "F" });
            await db.ForceSaveChangesAsync();
        });

        // Assert
        await WithDbContext(async db =>
        {
            List<Guid> tenantIds = await db.MemberCategories.IgnoreQueryFilters()
                .Where(x => x.Id == syncId || x.Id == forceId).Select(x => x.TenantId).ToListAsync();
            tenantIds.Count.ShouldBe(2);
            tenantIds.ShouldAllBe(id => id == otherTenant);
        });
    }

    [Fact]
    public async Task Queries_OnlyReturnRowsOfTheCurrentTenant()
    {
        // Arrange
        Guid otherTenant = await CreateTenantAsync();
        await WithDbContext(async db =>
        {
            db.MemberCategories.Add(new MemberCategoryEntity { Category = "own", Name = "own" });
            await db.SaveChangesAsync();
        });
        await WithTenantContext(otherTenant, async db =>
        {
            db.MemberCategories.Add(new MemberCategoryEntity { Category = "foreign", Name = "foreign" });
            await db.SaveChangesAsync();
        });

        // Act
        List<string> ownNames = [];
        List<string> otherNames = [];
        await WithDbContext(async db =>
            ownNames = await db.MemberCategories.Select(x => x.Name).ToListAsync());
        await WithTenantContext(otherTenant, async db =>
            otherNames = await db.MemberCategories.Select(x => x.Name).ToListAsync());

        // Assert
        ownNames.ShouldContain("own");
        ownNames.ShouldNotContain("foreign");
        otherNames.ShouldBe(["foreign"]);
    }

    [Fact]
    public async Task IgnoringTheSoftDeleteFilter_KeepsTheTenantFilter()
    {
        // Arrange
        Guid otherTenant = await CreateTenantAsync();
        Guid ownDeletedId = Guid.NewGuid();
        Guid foreignDeletedId = Guid.NewGuid();
        await WithDbContext(async db =>
        {
            db.MemberCategories.Add(new MemberCategoryEntity { Id = ownDeletedId, Category = "own", Name = "own", DeletedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        });
        await WithTenantContext(otherTenant, async db =>
        {
            db.MemberCategories.Add(new MemberCategoryEntity { Id = foreignDeletedId, Category = "foreign", Name = "foreign", DeletedAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        });

        // Act
        List<Guid> visibleIds = [];
        await WithDbContext(async db =>
            visibleIds = await db.MemberCategories.IgnoreQueryFilters(QueryFilter.SOFT_DELETE).Select(x => x.Id).ToListAsync());

        // Assert
        visibleIds.ShouldContain(ownDeletedId);
        visibleIds.ShouldNotContain(foreignDeletedId);
    }

    [Fact]
    public async Task BookingNumbers_CanRepeatTheSameSequenceInAnotherTenant()
    {
        // Arrange
        Guid otherTenant = await CreateTenantAsync();

        // Act
        await WithDbContext(async db =>
        {
            db.BookingNumbers.Add(new BookingNumberEntity { Year = 2026, Kind = BookingKind.INCOME, Sequence = 1 });
            await db.SaveChangesAsync();
        });
        Func<Task> otherTenantInsert = () => WithTenantContext(otherTenant, async db =>
        {
            db.BookingNumbers.Add(new BookingNumberEntity { Year = 2026, Kind = BookingKind.INCOME, Sequence = 1 });
            await db.SaveChangesAsync();
        });
        Func<Task> sameTenantInsert = () => WithDbContext(async db =>
        {
            db.BookingNumbers.Add(new BookingNumberEntity { Year = 2026, Kind = BookingKind.INCOME, Sequence = 1 });
            await db.SaveChangesAsync();
        });

        // Assert
        await Should.NotThrowAsync(otherTenantInsert);
        await Should.ThrowAsync<DbUpdateException>(sameTenantInsert);
    }

    [Fact]
    public async Task ForEachTenantAsync_VisitsEveryActiveTenantOnceWithItsOwnContext()
    {
        // Arrange
        Guid otherTenant = await CreateTenantAsync();
        Guid deletedTenant = await CreateTenantAsync();
        await WithDbContext(async db =>
        {
            TenantEntity tenant = await db.Tenants.SingleAsync(t => t.Id == deletedTenant);
            tenant.DeletedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        });
        List<Guid> visited = [];

        // Act
        await Factory.Services.GetRequiredService<IServiceScopeFactory>().ForEachTenantAsync(services =>
        {
            visited.Add(services.GetRequiredService<AppDbContext>().CurrentTenantId);
            return Task.CompletedTask;
        });

        // Assert
        visited.ShouldContain(TenantEntity.DefaultId);
        visited.ShouldContain(otherTenant);
        visited.ShouldNotContain(deletedTenant);
        visited.Distinct().Count().ShouldBe(visited.Count);
    }

    [Fact]
    public async Task Login_IssuesATokenWithTheTenantOfTheUser()
    {
        // Arrange
        (_, Guid userId) = await CreateUserAndClientAsync(UserRole.USER, "tenant-token-user");
        UserEntity user = null!;
        await WithDbContext(async db => user = await db.Users.SingleAsync(u => u.Id == userId));

        // Act
        string token = GetService<JwtService>().CreateToken(user);

        // Assert
        new JwtSecurityTokenHandler().ReadJwtToken(token).Claims
            .Single(c => c.Type == JwtService.TenantClaim).Value.ShouldBe(TenantEntity.DefaultId.ToString());
    }

    [Fact]
    public async Task Token_OfAnotherTenant_IsRejected()
    {
        // Arrange
        Guid otherTenant = await CreateTenantAsync();
        Guid userId = EnsureActorUser(UserRole.USER);
        IConfiguration configuration = GetService<IConfiguration>();

        HttpClient foreign = CreateClient().WithBearerToken(
            JwtTestHelper.CreateToken(configuration, userId, "user", UserRole.USER, tenantId: otherTenant));
        HttpClient own = CreateClient().WithBearerToken(
            JwtTestHelper.CreateToken(configuration, userId, "user", UserRole.USER, tenantId: TenantEntity.DefaultId));
        HttpClient withoutClaim = CreateClient().WithBearerToken(
            JwtTestHelper.CreateToken(configuration, userId, "user", UserRole.USER));

        // Act
        HttpResponseMessage foreignResponse = await foreign.GetAsync("/auth/me");
        HttpResponseMessage ownResponse = await own.GetAsync("/auth/me");
        HttpResponseMessage withoutClaimResponse = await withoutClaim.GetAsync("/auth/me");

        // Assert
        foreignResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        ownResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        withoutClaimResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task User_OfAnotherTenant_CannotBeSeenOrLoggedInByTheCurrentTenant()
    {
        // Arrange
        Guid otherTenant = await CreateTenantAsync();
        Guid foreignUserId = Guid.NewGuid();
        await WithTenantContext(otherTenant, async db =>
        {
            db.Users.Add(new UserEntity
            {
                Id = foreignUserId,
                Username = "foreign-user",
                PasswordHash = CryptoService.HashPassword("Test1234!"),
                Role = UserRole.USER
            });
            await db.SaveChangesAsync();
        });
        IConfiguration configuration = GetService<IConfiguration>();
        HttpClient client = CreateClient().WithBearerToken(
            JwtTestHelper.CreateToken(configuration, foreignUserId, "foreign-user", UserRole.USER));

        // Act
        HttpResponseMessage response = await client.GetAsync("/auth/me");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
