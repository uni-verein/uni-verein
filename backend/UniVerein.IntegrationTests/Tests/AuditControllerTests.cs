using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Data.Enums;
using UniVerein.Api.Query;
using UniVerein.Api.Services;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using UniVerein.IntegrationTests.Infrastructure;
using Shouldly;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class AuditControllerTests : IntegrationTestBase
{
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public AuditControllerTests(UniVereinWebApplicationFactory factory) : base(factory)
    {
        _jsonSerializerOptions = new()
        {
            Converters = { new JsonStringEnumConverter() }
        };
    }

    public override async Task InitializeAsync()
    {
        await WithDbContext(async db =>
        {
            db.AuditLogs.RemoveRange(db.AuditLogs.AsQueryable());
            await db.ForceSaveChangesAsync();
        });
    }

    public override async Task DisposeAsync()
    {
        await WithDbContext(async db =>
        {
            db.AuditLogs.RemoveRange(db.AuditLogs.AsQueryable());
            await db.ForceSaveChangesAsync();
        });
    }

    [Fact]
    public async Task GetAllAuditLogs_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/audit");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task GetAllAuditLogs_Forbidden(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response = await client.GetAsync("/audit");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAllAuditLogs_Authorized()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        foreach (var index in Enumerable.Range(0, 5))
            await CreateAuditLogEntity(index.ToString());

        // Act
        HttpResponseMessage response = await client.GetAsync("/audit");
        AuditLogResults? results = await response.Content.ReadFromJsonAsync<AuditLogResults>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        results.ShouldNotBeNull();
        results!.Total.ShouldBe(5);
        results.Items.Select(x => int.Parse(x.Data)).ToArray().ShouldBeEquivalentTo(Enumerable.Range(0, 5).ToArray());
    }

    [Fact]
    public async Task GetAllAuditLogs_Success()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        List<AuditLogEntity> auditLogs = new();
        foreach (var index in Enumerable.Range(0, 5))
            auditLogs.Add(await CreateAuditLogEntity(index.ToString()));

        // Act
        HttpResponseMessage response = await client.GetAsync("/audit");
        AuditLogResults? results = await response.Content.ReadFromJsonAsync<AuditLogResults>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        results.ShouldNotBeNull();
        results!.Total.ShouldBe(5);
        foreach (AuditLogEntity auditLog in auditLogs)
        {
            AuditLogResult? result = results!.Items.FirstOrDefault(x => x.Data == auditLog.Data);
            result.ShouldNotBeNull();
            CompareAuditLog(auditLog, result!);
        }

        results.Items.Select(x => int.Parse(x.Data)).ToArray().ShouldBeEquivalentTo(Enumerable.Range(0, 5).ToArray());
    }

    [Fact]
    public async Task GetAllAuditLogs_paging_Success()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        List<AuditLogEntity> auditLogs = new();
        foreach (var index in Enumerable.Range(0, 5))
        {
            auditLogs.Add(await CreateAuditLogEntity(index.ToString()));
            Factory.FakeTime.SetUtcNow(DateTimeOffset.UtcNow.AddSeconds(index));
        }

        AuditLogQuery auditLogQuery = new()
        {
            Offset = 1,
            Limit = 1,
        };

        // Act
        HttpResponseMessage response = await client.GetAsync($"/audit{auditLogQuery.GetQueryString()}");
        AuditLogResults? results = await response.Content.ReadFromJsonAsync<AuditLogResults>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        results.ShouldNotBeNull();
        results!.Total.ShouldBe(5);
        AuditLogResult? result = results!.Items.FirstOrDefault();
        result.ShouldNotBeNull();
        CompareAuditLog(auditLogs.First(x => x.Data == "3"), result!);
    }

    [Theory]
    [InlineData(-1, -1)]
    [InlineData(0, 0)]
    [InlineData(10, -1)]
    public async Task GetAllAuditLogs_LimitOrOffset_BadRequest(int limit, int offset)
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        AuditLogQuery auditLogQuery = new()
        {
            Offset = offset,
            Limit = limit
        };

        // Act
        HttpResponseMessage response = await client.GetAsync($"/audit{auditLogQuery.GetQueryString()}");
        ErrorDetailsResult? results =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        results.ShouldNotBeNull();
        results!.ErrorMessage.ShouldBe("Failed request validation");
        results!.MoreInfo.ShouldBe("Offset and/or Limit must be greater than or equal to 1.");
    }

    private async Task<AuditLogEntity> CreateAuditLogEntity(string? data = null)
    {
        UserEntity userEntity = new()
        {
            Id = Guid.NewGuid(),
            Username = Guid.NewGuid().ToString(),
            PasswordHash = CryptoService.HashPassword(Guid.NewGuid().ToString()),
            Role = UserRole.USER,
        };

        AuditLogEntity auditLogEntity = new()
        {
            UserId = userEntity.Id,
            User = userEntity,
            Action = nameof(AuditLogActions.CREATE),
            Entity = nameof(CreateAuditLogEntity),
            Data = data ?? "Test data"
        };

        await WithDbContext(async db =>
        {
            await db.Users.AddAsync(userEntity);
            await db.AuditLogs.AddAsync(auditLogEntity);
            await db.SaveChangesAsync();
        });

        return auditLogEntity;
    }

    private void CompareAuditLog(AuditLogEntity entity, AuditLogResult result)
    {
        TimeSpan tolerance = TimeSpan.FromMilliseconds(2);
        Assert.True(Math.Abs((entity.CreatedAt - result.Timestamp).TotalMilliseconds) < tolerance.TotalMilliseconds);
        entity.User.Username.ShouldBe(result.UserName);
        entity.Action.ShouldBe(result.Action);
        entity.Entity.ShouldBe(result.Entity);
        entity.Data.ShouldBe(result.Data);
    }
}