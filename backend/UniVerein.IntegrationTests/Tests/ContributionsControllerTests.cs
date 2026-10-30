using UniVerein.Api.Models.Enums;
using System.Net;
using System.Net.Http.Json;
using UniVerein.Api.ApiRequests;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Exceptions;
using UniVerein.Api.Query;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using UniVerein.IntegrationTests.Infrastructure;
using Shouldly;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class ContributionsControllerTests : IntegrationTestBase
{
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    public ContributionsControllerTests(UniVereinWebApplicationFactory factory)
        : base(factory)
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
            db.Members.RemoveRange(db.Members.AsQueryable());
            db.Contributions.RemoveRange(db.Contributions.AsQueryable());
            await db.ForceSaveChangesAsync();
        });
    }

    public override async Task DisposeAsync()
    {
        await WithDbContext(async db =>
        {
            db.Members.RemoveRange(db.Members.AsQueryable());
            db.Contributions.RemoveRange(db.Contributions.AsQueryable());
            await db.ForceSaveChangesAsync();
        });
    }

    // ---------------------------------------------------------------
    // GET /api/contributions/info
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetContributionInfo_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/contributions/info");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task GetContributionInfo_Authorized(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);
        List<(MemberEntity, ContributionEntity)> members = new();
        foreach (int index in Enumerable.Range(0, 5))
            members.Add(await CreateContributionEntity(index.ToString(), index.ToString()));
        decimal amount = members.Select(x => x.Item2.Amount).Sum();

        // Act
        HttpResponseMessage response = await client.GetAsync("/contributions/info");
        ContributionInfoResult? result =
            await response.Content.ReadFromJsonAsync<ContributionInfoResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result!.OpenPayments.ShouldBe(members.Count);
        result.OpenAmount.ShouldBe(amount);
    }

    // ---------------------------------------------------------------
    // GET /api/contributions
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetAllContributions_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/contributions");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task GetAllContributions_Authorized(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);
        List<(MemberEntity, ContributionEntity)> members = new();
        foreach (int index in Enumerable.Range(0, 5))
            members.Add(await CreateContributionEntity(index.ToString(), index.ToString()));

        // Act
        HttpResponseMessage response = await client.GetAsync("/contributions");
        AllContributionsResult? results =
            await response.Content.ReadFromJsonAsync<AllContributionsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        results.ShouldNotBeNull();
        results!.Total.ShouldBe(5);
        foreach ((MemberEntity member, ContributionEntity contribution) in members)
        {
            ContributionResult? result = results.Items.Find(x => x.Id == contribution.Id);
            result.ShouldNotBeNull();
            result!.Name.ShouldBe($"{member.FirstName} {member.LastName}");
            result.Amount.ShouldBe(contribution.Amount);
            result.Paid.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task GetAllContributions_paging_Success()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.USER);
        List<(MemberEntity, ContributionEntity)> members = new();
        foreach (int index in Enumerable.Range(0, 5))
            members.Add(await CreateContributionEntity(index.ToString(), index.ToString()));

        ContributionsQuery contributionsQuery = new()
        {
            Offset = 1,
            Limit = 1,
        };

        // Act
        HttpResponseMessage response = await client.GetAsync($"/contributions{contributionsQuery.GetQueryString()}");
        AllContributionsResult? results =
            await response.Content.ReadFromJsonAsync<AllContributionsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        results.ShouldNotBeNull();
        results!.Total.ShouldBe(5);
        results.Items.Count.ShouldBe(1);
        ContributionResult? result = results.Items.FirstOrDefault();
        result.ShouldNotBeNull();

        members.First(x => x.Item1.FirstName == "1").Item2.Id.ShouldBe(result!.Id);
    }

    [Theory]
    [InlineData(null, "test")]
    [InlineData(false, null)]
    public async Task GetAllContributions_byQueryParameter_Success(bool? unpaid, string? name)
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.USER);
        foreach (int index in Enumerable.Range(0, 5))
            await CreateContributionEntity(index.ToString(), index.ToString());
        (MemberEntity _, ContributionEntity contribution) = await CreateContributionEntity("test", "test", true);

        ContributionsQuery contributionsQuery = new()
        {
            Unpaid = unpaid,
            Name = name,
        };

        // Act
        HttpResponseMessage response = await client.GetAsync($"/contributions{contributionsQuery.GetQueryString()}");
        AllContributionsResult? results =
            await response.Content.ReadFromJsonAsync<AllContributionsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        results.ShouldNotBeNull();
        results!.Total.ShouldBe(1);
        results.Items.Count.ShouldBe(1);
        ContributionResult? result = results.Items.FirstOrDefault();
        result.ShouldNotBeNull();

        result!.Id.ShouldBe(contribution.Id);
    }

    [Fact]
    public async Task GetAllContributions_NameSearch_MatchesSubstringInName()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.USER);
        foreach (int index in Enumerable.Range(0, 3))
            await CreateContributionEntity(index.ToString(), index.ToString());
        (MemberEntity _, ContributionEntity contribution) = await CreateContributionEntity("rene", "tester");

        ContributionsQuery contributionsQuery = new() { Name = "test" };

        // Act
        HttpResponseMessage response = await client.GetAsync($"/contributions{contributionsQuery.GetQueryString()}");
        AllContributionsResult? results =
            await response.Content.ReadFromJsonAsync<AllContributionsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        results.ShouldNotBeNull();
        results!.Total.ShouldBe(1);
        results.Items.Single().Id.ShouldBe(contribution.Id);
    }

    [Fact]
    public async Task GetAllContributions_ReturnsMemberNumberAndMembershipStatus()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.USER);
        (MemberEntity _, ContributionEntity contribution) =
            await CreateContributionEntity("a", "b", membershipStatus: MembershipStatus.SUPPORTING_MEMBER,
                memberNumber: 42);

        // Act
        HttpResponseMessage response = await client.GetAsync("/contributions");
        AllContributionsResult? results =
            await response.Content.ReadFromJsonAsync<AllContributionsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        results.ShouldNotBeNull();
        ContributionResult? result = results!.Items.Find(x => x.Id == contribution.Id);
        result.ShouldNotBeNull();
        result!.MemberNumber.ShouldBe(42);
        result.MembershipStatus.ShouldBe(MembershipStatus.SUPPORTING_MEMBER);
    }

    [Theory]
    [InlineData(MembershipStatus.MEMBER)]
    [InlineData(MembershipStatus.SUPPORTING_MEMBER)]
    public async Task GetAllContributions_byMembershipStatus_Success(MembershipStatus status)
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.USER);
        (MemberEntity _, ContributionEntity member) =
            await CreateContributionEntity("m", "m", membershipStatus: MembershipStatus.MEMBER);
        (MemberEntity _, ContributionEntity supporter) =
            await CreateContributionEntity("s", "s", membershipStatus: MembershipStatus.SUPPORTING_MEMBER);

        ContributionsQuery contributionsQuery = new() { MembershipStatus = status };

        // Act
        HttpResponseMessage response = await client.GetAsync($"/contributions{contributionsQuery.GetQueryString()}");
        AllContributionsResult? results =
            await response.Content.ReadFromJsonAsync<AllContributionsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        results.ShouldNotBeNull();
        results!.Total.ShouldBe(1);
        results.Items.Single().Id.ShouldBe(status == MembershipStatus.MEMBER ? member.Id : supporter.Id);
    }

    [Fact]
    public async Task GetAllContributions_InvalidMembershipStatus_BadRequest()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.USER);

        // Act
        HttpResponseMessage response = await client.GetAsync("/contributions?membershipStatus=INVALID");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task GetAllContributions_DeletedFilter_IgnoredForNonAdmins(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);
        await CreateContributionEntity("active", "active");
        await CreateContributionEntity("gone", "gone", deleted: true);

        // Act
        HttpResponseMessage response = await client.GetAsync("/contributions?deleted=true");
        AllContributionsResult? results =
            await response.Content.ReadFromJsonAsync<AllContributionsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        results.ShouldNotBeNull();
        results!.Total.ShouldBe(1);
        results.Items.Single().Name.ShouldBe("active active");
    }

    [Fact]
    public async Task GetAllContributions_DeletedFilter_AdminSeesOnlyDeleted()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        await CreateContributionEntity("active", "active");
        (MemberEntity _, ContributionEntity deleted) = await CreateContributionEntity("gone", "gone", deleted: true);

        // Act
        HttpResponseMessage response = await client.GetAsync("/contributions?deleted=true");
        AllContributionsResult? results =
            await response.Content.ReadFromJsonAsync<AllContributionsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        results.ShouldNotBeNull();
        results!.Total.ShouldBe(1);
        results.Items.Single().Id.ShouldBe(deleted.Id);
        results.Items.Single().DeletedAt.ShouldNotBeNull();
    }

    // ---------------------------------------------------------------
    // DELETE /contributions/{id}
    // ---------------------------------------------------------------

    [Fact]
    public async Task Delete_WithoutToken_Unauthorized()
    {
        HttpClient client = CreateClient();

        HttpResponseMessage response = await client.DeleteAsync($"/contributions/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_Forbidden()
    {
        HttpClient client = CreateClient(UserRole.USER);

        HttpResponseMessage response = await client.DeleteAsync($"/contributions/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task Delete_NotFound(UserRole role)
    {
        HttpClient client = CreateClient(role);

        HttpResponseMessage response = await client.DeleteAsync($"/contributions/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task Delete_PaidContribution_BadRequest(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);
        (MemberEntity _, ContributionEntity contribution) = await CreateContributionEntity(paid: true);

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/contributions/{contribution.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await WithDbContext(async db =>
        {
            ContributionEntity? entity = await db.Contributions.FindAsync(contribution.Id);
            entity.ShouldNotBeNull();
            entity!.DeletedAt.ShouldBeNull();
        });
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task Delete_OpenContribution_SoftDeletes(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);
        (MemberEntity _, ContributionEntity contribution) = await CreateContributionEntity();

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/contributions/{contribution.Id}");
        HttpResponseMessage listResponse = await client.GetAsync("/contributions");
        AllContributionsResult? results =
            await listResponse.Content.ReadFromJsonAsync<AllContributionsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        results.ShouldNotBeNull();
        results!.Total.ShouldBe(0);
        await WithDbContext(async db =>
        {
            ContributionEntity? entity = await db.Contributions.FindAsync(contribution.Id);
            entity.ShouldNotBeNull();
            entity!.DeletedAt.ShouldNotBeNull();
        });
    }

    // ---------------------------------------------------------------
    // POST /contributions/{id}/restore
    // ---------------------------------------------------------------

    [Fact]
    public async Task Restore_WithoutToken_Unauthorized()
    {
        HttpClient client = CreateClient();

        HttpResponseMessage response =
            await client.PostAsJsonAsync($"/contributions/{Guid.NewGuid()}/restore", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task Restore_Forbidden(UserRole role)
    {
        HttpClient client = CreateClient(role);

        HttpResponseMessage response =
            await client.PostAsJsonAsync($"/contributions/{Guid.NewGuid()}/restore", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Restore_NotFound()
    {
        HttpClient client = CreateClient(UserRole.ADMIN);

        HttpResponseMessage response =
            await client.PostAsJsonAsync($"/contributions/{Guid.NewGuid()}/restore", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Restore_Success()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        (MemberEntity _, ContributionEntity contribution) = await CreateContributionEntity(deleted: true);

        // Act
        HttpResponseMessage response =
            await client.PostAsJsonAsync($"/contributions/{contribution.Id}/restore", new { });
        HttpResponseMessage listResponse = await client.GetAsync("/contributions");
        AllContributionsResult? results =
            await listResponse.Content.ReadFromJsonAsync<AllContributionsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        results.ShouldNotBeNull();
        results!.Items.ShouldContain(x => x.Id == contribution.Id);
    }

    // ---------------------------------------------------------------
    // PAID /api/{id}/contributions
    // ---------------------------------------------------------------

    [Fact]
    public async Task MarkAsPaid_WithoutToken_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response =
            await client.PostAsJsonAsync($"/contributions/{Guid.NewGuid()}", new MarkPaidRequest { Paid = true });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task MarkAsPaid_Forbidden()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.USER);

        // Act
        HttpResponseMessage response =
            await client.PostAsJsonAsync($"/contributions/{Guid.NewGuid()}", new MarkPaidRequest { Paid = true });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task MarkAsPaid_NotFound(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response =
            await client.PostAsJsonAsync($"/contributions/{Guid.NewGuid()}", new MarkPaidRequest { Paid = true });
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.ShouldNotBeNull();
        result!.StatusCode.ShouldBe((int)HttpStatusCode.NotFound);
        result.ErrorCode.ShouldBe(ApiErrorCodes.RESOURCE_NOT_FOUND);
        result.ErrorMessage.ShouldBe("Contribution config not found.");
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task MarkAsPaid_Success(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);
        (MemberEntity _, ContributionEntity contribution) = await CreateContributionEntity();

        // Act
        HttpResponseMessage response =
            await client.PostAsJsonAsync($"/contributions/{contribution.Id}", new MarkPaidRequest { Paid = true });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await WithDbContext(async db =>
        {
            ContributionEntity? contributionEntity = await db.Contributions.FindAsync(contribution.Id);
            contributionEntity.ShouldNotBeNull();
            contributionEntity!.Paid.ShouldNotBeNull();
        });
    }

    [Fact]
    public async Task MarkAsPaid_DeletedContribution_ReturnsNotFoundAndKeepsItOpen()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        (MemberEntity _, ContributionEntity contribution) = await CreateContributionEntity(deleted: true);

        // Act
        HttpResponseMessage response =
            await client.PostAsJsonAsync($"/contributions/{contribution.Id}", new MarkPaidRequest { Paid = true });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await WithDbContext(async db =>
        {
            ContributionEntity? entity = await db.Contributions.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == contribution.Id);
            entity!.Paid.ShouldBeNull();
            (await db.BookingNumbers.AnyAsync(x => x.ContributionId == contribution.Id)).ShouldBeFalse();
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MarkAsPaid_WritesAuditLog(bool paid)
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.FINANCIAL_MANAGER);
        (MemberEntity _, ContributionEntity contribution) = await CreateContributionEntity(paid: !paid);

        // Act
        HttpResponseMessage response =
            await client.PostAsJsonAsync($"/contributions/{contribution.Id}", new MarkPaidRequest { Paid = paid });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await WithDbContext(async db =>
        {
            AuditLogEntity? auditLog = await db.AuditLogs.FirstOrDefaultAsync(l =>
                l.Entity == nameof(ContributionEntity) && l.Data.Contains(contribution.Id.ToString()));
            auditLog.ShouldNotBeNull();
            auditLog!.Action.ShouldBe(nameof(AuditLogActions.UPDATE));
            auditLog.Data.ShouldContain($"\"Paid\":{paid.ToString().ToLowerInvariant()}");
        });
    }

    // ---------------------------------------------------------------
    // Helper functions
    // ---------------------------------------------------------------

    private async Task<(MemberEntity, ContributionEntity)> CreateContributionEntity(string? firstName = null,
        string? lastName = null, bool? paid = null, MembershipStatus membershipStatus = MembershipStatus.MEMBER,
        int memberNumber = 0, bool deleted = false)
    {
        Guid memberId = Guid.NewGuid();
        MemberEntity memberEntity = new()
        {
            MandateId = memberId.ToString(),
            Id = memberId,
            FirstName = firstName ?? Guid.NewGuid().ToString(),
            LastName = lastName ?? Guid.NewGuid().ToString(),
            BirthdayEncrypted = string.Empty,
            City = "Musterstadt",
            PostalCode = "12345",
            CountryCode = "DE",
            StreetEncrypted = string.Empty,
            MembershipStatus = membershipStatus,
            MemberNumber = memberNumber
        };

        ContributionEntity contributionEntity = new()
        {
            Id = Guid.NewGuid(),
            MemberId = memberEntity.Id,
            MemberEntity = memberEntity,
            Amount = Random.Shared.Next(12, 1000),
            DueDate = DateTime.Now,
            Paid = paid == true ? DateTimeOffset.UtcNow : null,
            DeletedAt = deleted ? DateTimeOffset.UtcNow : null
        };

        await WithDbContext(async db =>
        {
            await db.Members.AddAsync(memberEntity);
            await db.Contributions.AddAsync(contributionEntity);
            await db.SaveChangesAsync();
        });

        return (memberEntity, contributionEntity);
    }
}