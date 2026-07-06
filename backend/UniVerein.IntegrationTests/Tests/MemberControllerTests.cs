using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Data.Enums;
using UniVerein.Api.Exceptions;
using UniVerein.Api.Query;
using UniVerein.Api.Services;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using UniVerein.IntegrationTests.Infrastructure;
using Shouldly;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class MemberControllerTests : IntegrationTestBase
{
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private readonly CryptoService _cryptoService;

    public MemberControllerTests(UniVereinWebApplicationFactory factory) : base(factory)
    {
        _jsonSerializerOptions = new()
        {
            Converters = { new JsonStringEnumConverter() }
        };
        _cryptoService = GetService<CryptoService>();
    }

    public override async Task InitializeAsync()
    {
        await WithDbContext(async db =>
        {
            db.Members.RemoveRange(db.Members.AsQueryable());
            await db.ForceSaveChangesAsync();
        });
    }

    public override async Task DisposeAsync()
    {
        await WithDbContext(async db =>
        {
            db.Members.RemoveRange(db.Members.AsQueryable());
            await db.ForceSaveChangesAsync();
        });
    }

    [Fact]
    public async Task GetAllMembers_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/members");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task GetAllMembers_Authorized(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);
        foreach (var index in Enumerable.Range(0, 5))
            await CreateMemberEntity(index);

        // Act
        HttpResponseMessage response = await client.GetAsync("/members");
        AllMemberResults? results = await response.Content.ReadFromJsonAsync<AllMemberResults>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        results.ShouldNotBeNull();
        results.Total.ShouldBe(5);
        results.Items.Select(x => x.MemberNumber).ToArray().ShouldBeEquivalentTo(Enumerable.Range(0, 5).ToArray());
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task GetAllMembers_Success(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);
        List<MemberEntity> members = new();
        foreach (var index in Enumerable.Range(0, 5))
            members.Add(await CreateMemberEntity(index));

        // Act
        HttpResponseMessage response = await client.GetAsync("/members");
        AllMemberResults? results = await response.Content.ReadFromJsonAsync<AllMemberResults>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        results.ShouldNotBeNull();
        results.Total.ShouldBe(5);
        foreach (MemberEntity member in members)
        {
            MemberResult? result = results.Items.FirstOrDefault(x => x.MemberNumber == member.MemberNumber);
            result.ShouldNotBeNull();
            CompareMember(member, result);
        }

        results.Items.Select(x => x.MemberNumber).ToArray().ShouldBeEquivalentTo(Enumerable.Range(0, 5).ToArray());
    }

    [Theory]
    [InlineData(UserRole.ADMIN, true, null, null, null)]
    [InlineData(UserRole.USER, true, null, null, null)]
    [InlineData(UserRole.FINANCIAL_MANAGER, true, null, null, null)]
    [InlineData(UserRole.ADMIN, false, TaskWithinTheClub.ALUMNI_OFFICER, null, null)]
    [InlineData(UserRole.USER, false, TaskWithinTheClub.ALUMNI_OFFICER, null, null)]
    [InlineData(UserRole.FINANCIAL_MANAGER, false, TaskWithinTheClub.ALUMNI_OFFICER, null, null)]
    [InlineData(UserRole.ADMIN, false, null, Program.MemberCategoriesOther, null)]
    [InlineData(UserRole.USER, false, null, Program.MemberCategoriesOther, null)]
    [InlineData(UserRole.FINANCIAL_MANAGER, false, null, Program.MemberCategoriesOther, null)]
    [InlineData(UserRole.ADMIN, false, null, null, true)]
    public async Task GetAllMembers_Filter_Success(UserRole role, bool byName, TaskWithinTheClub? taskWithinTheClub,
        string? memberCategory, bool? deleted)
    {
        // Arrange
        string testName = Guid.NewGuid().ToString();
        HttpClient client = CreateClient(role);
        foreach (var index in Enumerable.Range(0, 5))
            await CreateMemberEntity(index);

        Guid? memberCategoryId = string.IsNullOrWhiteSpace(memberCategory) ? null : Guid.Parse(memberCategory);
        MemberEntity member =
            await CreateMemberEntity(10, byName ? testName : null, taskWithinTheClub, memberCategoryId, deleted);
        MemberQuery memberQuery = new()
        {
            Name = byName ? testName : null,
            TaskWithinTheClub = taskWithinTheClub,
            MemberCategoryId = memberCategoryId,
            Deleted = deleted
        };

        // Act
        HttpResponseMessage response = await client.GetAsync($"/members{memberQuery.GetQueryString()}");
        AllMemberResults? results = await response.Content.ReadFromJsonAsync<AllMemberResults>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        results.ShouldNotBeNull();
        results.Total.ShouldBe(1);
        MemberResult? result = results.Items.FirstOrDefault();
        result.ShouldNotBeNull();
        CompareMember(member, result);
    }

    [Fact]
    public async Task GetAllMembers_paging_Success()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.USER);
        List<MemberEntity> members = new();
        foreach (var index in Enumerable.Range(0, 5))
            members.Add(await CreateMemberEntity(index));

        MemberQuery memberQuery = new()
        {
            Offset = 1,
            Limit = 1,
        };

        // Act
        HttpResponseMessage response = await client.GetAsync($"/members{memberQuery.GetQueryString()}");
        AllMemberResults? results = await response.Content.ReadFromJsonAsync<AllMemberResults>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        results.ShouldNotBeNull();
        results.Total.ShouldBe(5);
        MemberResult? result = results.Items.FirstOrDefault();
        result.ShouldNotBeNull();
        CompareMember(members.First(x => x.MemberNumber == 1), result);
    }

    [Theory]
    [InlineData(-1, -1)]
    [InlineData(0, 0)]
    [InlineData(10, -1)]
    public async Task GetAllMembers_LimitOrOffset_BadRequest(int limit, int offset)
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.USER);
        MemberQuery memberQuery = new()
        {
            Offset = offset,
            Limit = limit
        };

        // Act
        HttpResponseMessage response = await client.GetAsync($"/members{memberQuery.GetQueryString()}");
        ErrorDetailsResult? results =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        results.ShouldNotBeNull();
        results.ErrorMessage.ShouldBe("Failed request validation");
        results.MoreInfo.ShouldBe("Offset and/or Limit must be greater than or equal to 1.");
    }

    [Fact]
    public async Task GetMemberCount_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/members/count");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task GetMemberCount_Authorized(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response = await client.GetAsync("/members/count");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task GetMemberCount_Success(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);
        foreach (var index in Enumerable.Range(0, 5))
            await CreateMemberEntity(index);

        // Act
        HttpResponseMessage response = await client.GetAsync("/members/count");
        MemberCountResult? result = await response.Content.ReadFromJsonAsync<MemberCountResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.Count.ShouldBe(5);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task GetMemberCount_byCategory_Success(UserRole role)
    {
        // Arrange
        Guid testCategory = Guid.Parse(Program.MemberCategoriesAlumni);
        HttpClient client = CreateClient(role);
        await CreateMemberEntity(10, memberCategory: testCategory);
        foreach (var index in Enumerable.Range(0, 5))
            await CreateMemberEntity(index, memberCategory: Guid.Parse(Program.MemberCategoriesStudent));

        // Act
        HttpResponseMessage response = await client.GetAsync($"/members/count?memberCategoryId={testCategory}");
        MemberCountResult? result = await response.Content.ReadFromJsonAsync<MemberCountResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.Count.ShouldBe(1);
    }

    [Fact]
    public async Task CreateMember_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/members", CreateMemberRequest());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task CreateMember_Authorized(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/members", CreateMemberRequest());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Theory]
    [InlineData(UserRole.ADMIN, true, false)]
    [InlineData(UserRole.ADMIN, false, true)]
    [InlineData(UserRole.USER, true, false)]
    [InlineData(UserRole.USER, false, true)]
    [InlineData(UserRole.FINANCIAL_MANAGER, true, false)]
    [InlineData(UserRole.FINANCIAL_MANAGER, false, true)]
    public async Task CreateMember_WithDuplicateMember_Conflict(UserRole role, bool isSameEmail, bool isSameIban)
    {
        HttpClient client = CreateClient(role);
        MemberEntity memberEntity = await CreateMemberEntity();
        string email = _cryptoService.Decrypt(memberEntity.EmailEncrypted)!;
        string iban = _cryptoService.Decrypt(memberEntity.IBAN_Encrypted)!;
        MemberRequest memberRequest =
            CreateMemberRequest(email: isSameEmail ? email : null, iban: isSameIban ? iban : null);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/members", memberRequest);
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe((int)HttpStatusCode.Conflict);
        result.MoreInfo.ShouldBe("A member with the same email address or IBAN already exists.");
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task CreateMember_ContributionPlan_NotFound(UserRole role)
    {
        HttpClient client = CreateClient(role);
        MemberRequest memberRequest = CreateMemberRequest(contributionPlanId: Guid.NewGuid());

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/members", memberRequest);
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe((int)HttpStatusCode.NotFound);
        result.ErrorMessage.ShouldBe("Contribution plan not found.");
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task CreateMember_Success(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);
        ContributionPlanEntity contributionPlan = new()
        {
            Name = Guid.NewGuid().ToString(),
            Amount = 12,
            Interval = Interval.MONTHLY
        };
        await WithDbContext(async db =>
        {
            await db.ContributionPlans.AddAsync(contributionPlan);
            await db.SaveChangesAsync();
        });
        MemberRequest memberRequest = CreateMemberRequest(contributionPlanId: contributionPlan.Id);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/members", memberRequest);
        MemberResult? result = await response.Content.ReadFromJsonAsync<MemberResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        result.ShouldNotBeNull();
        await WithDbContext(async db =>
        {
            MemberEntity? member = await db.Members.FirstOrDefaultAsync(m => m.FirstName == memberRequest.FirstName);
            CompareMember(member!, result);
        });
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task CreateMember_AuditLogCreated_Success(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);
        await WithDbContext(async db =>
        {
            await db.Users.AddAsync(new UserEntity()
            {
                Id = UserId,
                Username = Guid.NewGuid().ToString(),
                PasswordHash = Guid.NewGuid().ToString(),
                Role = role
            });
            await db.SaveChangesAsync();
        });
        MemberRequest memberRequest = CreateMemberRequest();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/members", memberRequest);
        MemberResult? result = await response.Content.ReadFromJsonAsync<MemberResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        result.ShouldNotBeNull();
        await WithDbContext(async db =>
        {
            MemberEntity? member = await db.Members.FirstOrDefaultAsync(m => m.FirstName == memberRequest.FirstName);
            AuditLogEntity? auditLog =
                await db.AuditLogs.FirstOrDefaultAsync(l => l.Data.Contains(member!.Id.ToString()));
            auditLog.ShouldNotBeNull();
            auditLog.Action.ShouldBe(nameof(AuditLogActions.CREATE));
            auditLog.Entity.ShouldBe(nameof(MemberEntity));
            UserEntity? user = await db.Users.FindAsync(UserId);
            db.Remove(user!);
            await db.ForceSaveChangesAsync();
        });
    }

    [Fact]
    public async Task UpdateMember_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response =
            await client.PatchAsJsonAsync($"/members/{Guid.NewGuid()}", UpdateMemberRequest());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task UpdateMember_Authorized(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);
        MemberEntity member = await CreateMemberEntity();

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync($"/members/{member.Id}", UpdateMemberRequest());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task UpdateMember_Member_NotFound(UserRole role)
    {
        HttpClient client = CreateClient(role);
        MemberUpdateRequest memberRequest = UpdateMemberRequest();

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync($"/members/{Guid.NewGuid()}", memberRequest);
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe((int)HttpStatusCode.NotFound);
        result.ErrorMessage.ShouldBe("Member with ID not found.");
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task UpdateMember_ContributionPlan_NotFound(UserRole role)
    {
        HttpClient client = CreateClient(role);
        MemberEntity member = await CreateMemberEntity();
        MemberUpdateRequest memberRequest = UpdateMemberRequest(contributionPlanId: Guid.NewGuid());

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync($"/members/{member.Id}", memberRequest);
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe((int)HttpStatusCode.NotFound);
        result.ErrorMessage.ShouldBe("Contribution plan not found.");
    }

    [Theory]
    [InlineData(UserRole.ADMIN, true, false)]
    [InlineData(UserRole.ADMIN, false, true)]
    [InlineData(UserRole.USER, true, false)]
    [InlineData(UserRole.USER, false, true)]
    [InlineData(UserRole.FINANCIAL_MANAGER, true, false)]
    [InlineData(UserRole.FINANCIAL_MANAGER, false, true)]
    public async Task UpdateMember_WithDuplicateMember_Conflict(UserRole role, bool isSameEmail, bool isSameIban)
    {
        HttpClient client = CreateClient(role);
        MemberEntity member = await CreateMemberEntity();
        string email = _cryptoService.Decrypt(member.EmailEncrypted)!;
        string iban = _cryptoService.Decrypt(member.IBAN_Encrypted)!;
        MemberEntity updateMember = await CreateMemberEntity();
        MemberUpdateRequest memberRequest =
            UpdateMemberRequest(email: isSameEmail ? email : null, iban: isSameIban ? iban : null);

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync($"/members/{updateMember.Id}", memberRequest);
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe((int)HttpStatusCode.Conflict);
        result.MoreInfo.ShouldBe("A member with the same email address or IBAN already exists.");
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task UpdateMember_Success(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);
        MemberEntity member = await CreateMemberEntity();
        ContributionPlanEntity contributionPlan = new()
        {
            Name = Guid.NewGuid().ToString(),
            Amount = 12,
            Interval = Interval.MONTHLY
        };
        await WithDbContext(async db =>
        {
            await db.ContributionPlans.AddAsync(contributionPlan);
            await db.SaveChangesAsync();
        });
        MemberUpdateRequest updateRequest = UpdateMemberRequest(contributionPlanId: contributionPlan.Id);

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync($"/members/{member.Id}", updateRequest);
        MemberResult? result = await response.Content.ReadFromJsonAsync<MemberResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        await WithDbContext(async db =>
        {
            MemberEntity? dbMember = await db.Members.FirstOrDefaultAsync(m => m.FirstName == updateRequest.FirstName);
            CompareMember(dbMember!, result);
        });
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task UpdateMember_AuditLogCreated_Success(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);
        await WithDbContext(async db =>
        {
            await db.Users.AddAsync(new UserEntity()
            {
                Id = UserId,
                Username = Guid.NewGuid().ToString(),
                PasswordHash = Guid.NewGuid().ToString(),
                Role = role
            });
            await db.SaveChangesAsync();
        });
        MemberEntity member = await CreateMemberEntity();
        MemberUpdateRequest updateRequest = UpdateMemberRequest();

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync($"/members/{member.Id}", updateRequest);
        MemberResult? result = await response.Content.ReadFromJsonAsync<MemberResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        await WithDbContext(async db =>
        {
            MemberEntity? dbMember = await db.Members.FirstOrDefaultAsync(m => m.FirstName == updateRequest.FirstName);
            AuditLogEntity? auditLog =
                await db.AuditLogs.FirstOrDefaultAsync(l => l.Data.Contains(dbMember!.Id.ToString()));
            auditLog.ShouldNotBeNull();
            auditLog.Action.ShouldBe(nameof(AuditLogActions.UPDATE));
            auditLog.Entity.ShouldBe(nameof(MemberEntity));
            UserEntity? user = await db.Users.FindAsync(UserId);
            db.Remove(user!);
            await db.ForceSaveChangesAsync();
        });
    }

    [Fact]
    public async Task RestoreMember_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.PostAsync($"/members/{Guid.NewGuid()}", new StringContent(""));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task RestoreMember_Forbidden(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response = await client.PostAsync($"/members/{Guid.NewGuid()}", new StringContent(""));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RestoreMember_Authorized()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        MemberEntity member = await CreateMemberEntity();

        // Act
        HttpResponseMessage response = await client.PostAsync($"/members/{member.Id}", new StringContent(""));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RestoreMember_Success()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        MemberEntity member = await CreateMemberEntity(deleted: true);

        // Act
        HttpResponseMessage response = await client.PostAsync($"/members/{member.Id}", new StringContent(""));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await WithDbContext(async db =>
        {
            MemberEntity? updatedMember = await db.Members.FindAsync(member.Id);
            updatedMember!.DeletedAt.ShouldBe(null);
        });
    }

    [Fact]
    public async Task RestoreMember_MemberNotFound()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.PostAsync($"/members/{Guid.NewGuid()}", new StringContent(""));
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe((int)HttpStatusCode.NotFound);
        result.ErrorCode.ShouldBe(ApiErrorCodes.RESOURCE_NOT_FOUND);
        result.ErrorMessage.ShouldBe("Member with ID not found.");
    }

    [Fact]
    public async Task RestoreMember_AuditLogCreated_Success()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        await WithDbContext(async db =>
        {
            await db.Users.AddAsync(new UserEntity()
            {
                Id = UserId,
                Username = Guid.NewGuid().ToString(),
                PasswordHash = Guid.NewGuid().ToString(),
                Role = UserRole.ADMIN
            });
            await db.SaveChangesAsync();
        });
        MemberEntity member = await CreateMemberEntity(deleted: true);

        // Act
        HttpResponseMessage response = await client.PostAsync($"/members/{member.Id}", new StringContent(""));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await WithDbContext(async db =>
        {
            MemberEntity? updatedMember = await db.Members.FirstOrDefaultAsync(m => m.Id == member.Id);
            updatedMember.ShouldNotBeNull();
            updatedMember.DeletedAt.ShouldBeNull();
            AuditLogEntity? auditLog =
                await db.AuditLogs.FirstOrDefaultAsync(x => x.Data.Contains(member.Id.ToString()));
            auditLog.ShouldNotBeNull();
            auditLog.Action.ShouldBe(nameof(AuditLogActions.RESTORE));
            auditLog.Entity.ShouldBe(nameof(MemberEntity));
            UserEntity? user = await db.Users.FindAsync(UserId);
            db.Remove(user!);
            await db.ForceSaveChangesAsync();
        });
    }

    [Fact]
    public async Task DeleteMember_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/members/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task DeleteMember_NotFound(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/members/{Guid.NewGuid()}");
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe((int)HttpStatusCode.NotFound);
        result.ErrorCode.ShouldBe(ApiErrorCodes.RESOURCE_NOT_FOUND);
        result.ErrorMessage.ShouldBe("Member with ID not found.");
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task DeleteMember_Authorized(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);
        MemberEntity member = await CreateMemberEntity();

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/members/{member.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task DeleteMember_Success(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);
        MemberEntity member = await CreateMemberEntity();

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/members/{member.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await WithDbContext(async db =>
        {
            MemberEntity? deletedMember = await db.Members.FirstOrDefaultAsync(m => m.Id == member.Id);
            if (role == UserRole.ADMIN)
            {
                deletedMember.ShouldBeNull();
            }
            else
            {
                deletedMember.ShouldNotBeNull();
                deletedMember.DeletedAt.ShouldNotBeNull();
            }
        });
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task DeleteMember_AuditLogCreated_Success(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);
        await WithDbContext(async db =>
        {
            await db.Users.AddAsync(new UserEntity()
            {
                Id = UserId,
                Username = Guid.NewGuid().ToString(),
                PasswordHash = Guid.NewGuid().ToString(),
                Role = UserRole.ADMIN
            });
            await db.SaveChangesAsync();
        });
        MemberEntity member = await CreateMemberEntity();

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/members/{member.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await WithDbContext(async db =>
        {
            AuditLogEntity? auditLog =
                await db.AuditLogs.FirstOrDefaultAsync(l => l.Data.Contains(member.Id.ToString()));
            auditLog.ShouldNotBeNull();
            auditLog.Action.ShouldBe(role == UserRole.ADMIN
                ? nameof(AuditLogActions.DELETE)
                : nameof(AuditLogActions.SOFT_DELETE));
            auditLog.Entity.ShouldBe(nameof(MemberEntity));
            UserEntity? user = await db.Users.FindAsync(UserId);
            db.Remove(user!);
            await db.ForceSaveChangesAsync();
        });
    }

    private void CompareMember(MemberEntity entity, MemberResult result)
    {
        TimeSpan tolerance = TimeSpan.FromMilliseconds(2);
        entity.Id.ShouldBe(result.Id);
        entity.MemberNumber.ShouldBe(result.MemberNumber);
        entity.Gender.ShouldBe(result.Gender);
        entity.FirstName.ShouldBe(result.FirstName);
        entity.MiddleName.ShouldBe(result.MiddleName);
        entity.LastName.ShouldBe(result.LastName);
        _cryptoService.DecryptDate(entity.BirthdayEncrypted).ShouldBe(result.Birthday);
        _cryptoService.Decrypt(entity.StreetEncrypted).ShouldBe(result.Street);
        entity.PostalCode.ShouldBe(result.PostalCode);
        entity.City.ShouldBe(result.City);
        entity.CountryCode.ShouldBe(result.CountryCode);
        _cryptoService.Decrypt(entity.EmailEncrypted).ShouldBe(result.Email);
        entity.EmailHash.ShouldBe(_cryptoService.Hash(result.Email));
        _cryptoService.Decrypt(entity.PhoneEncrypted).ShouldBe(result.Phone);
        entity.BulkMail.ShouldBe(result.BulkMail);
        Assert.True(Math.Abs((entity.StartOfStudies - result.StartOfStudies).TotalMilliseconds) <
                    tolerance.TotalMilliseconds);
        entity.EndOfStudies.ShouldBe(result.EndOfStudies);
        entity.AcademicDegree.ShouldBe(result.AcademicDegree);
        entity.CourseOfStudy.ShouldBe(result.CourseOfStudy);
        entity.TaskWithinTheClub.ShouldBe(result.TaskWithinTheClub);
        entity.MemberCategoryId.ShouldBe(result.MemberCategoryId);
        _cryptoService.Decrypt(entity.IBAN_Encrypted).ShouldBe(result.IBAN);
        entity.IBAN_Hash.ShouldBe(_cryptoService.Hash(result.IBAN));
        _cryptoService.Decrypt(entity.Bic_Encrypted).ShouldBe(result.Bic);
        entity.SepaConsent.ShouldBe(result.SepaConsent);
        Assert.True(Math.Abs((entity.EntryDate - result.EntryDate).TotalMilliseconds) < tolerance.TotalMilliseconds);
        entity.ExitDate.ShouldBe(result.ExitDate);
        entity.ContributionPlanId.ShouldBe(result.ContributionPlanId);
    }

    private async Task<MemberEntity> CreateMemberEntity(int number = 1, string? firstName = null,
        TaskWithinTheClub? taskWithinTheClub = null, Guid? memberCategory = null, bool? deleted = null)
    {
        Guid memberId = Guid.NewGuid();
        MemberEntity member = new()
        {
            MandateId = memberId.ToString(),
            Id = memberId,
            MemberNumber = number,
            Gender = Gender.MALE,
            FirstName = firstName ?? Guid.NewGuid().ToString(),
            MiddleName = Guid.NewGuid().ToString(),
            LastName = Guid.NewGuid().ToString(),
            BirthdayEncrypted = _cryptoService.Encrypt(DateTimeOffset.UtcNow),
            StreetEncrypted = _cryptoService.Encrypt("street"),
            PostalCode = "24103",
            City = "Kiel",
            CountryCode = "DE",
            EmailEncrypted = _cryptoService.Encrypt("email@test.de"),
            EmailHash = _cryptoService.Hash("email@test.de"),
            PhoneEncrypted = _cryptoService.Encrypt("01512345678"),
            BulkMail = BulkMail.ALLOWED,
            StartOfStudies = DateTimeOffset.UtcNow,
            EndOfStudies = DateTimeOffset.UtcNow,
            AcademicDegree = AcademicDegree.BA,
            CourseOfStudy = "IT",
            TaskWithinTheClub = taskWithinTheClub ?? TaskWithinTheClub.MEMBER,
            MemberCategoryId = memberCategory ?? Guid.Parse(Program.MemberCategoriesAlumni),
            IBAN_Encrypted = _cryptoService.Encrypt("IBAN"),
            IBAN_Hash = _cryptoService.Hash("IBAN"),
            Bic_Encrypted = _cryptoService.Encrypt("DEUTDEDE123"),
            SepaConsent = DateTimeOffset.UtcNow,
            EntryDate = DateTimeOffset.UtcNow,
            ExitDate = DateTimeOffset.UtcNow,
            ContributionPlanId = null,
            DeletedAt = deleted == true ? DateTimeOffset.UtcNow : null
        };

        await WithDbContext(async db =>
        {
            await db.Members.AddAsync(member);
            await db.SaveChangesAsync();
        });

        return member;
    }

    private MemberRequest CreateMemberRequest(string? email = null, string? iban = null,
        Guid? contributionPlanId = null)
    {
        return new MemberRequest()
        {
            Gender = Gender.MALE,
            FirstName = Guid.NewGuid().ToString(),
            MiddleName = Guid.NewGuid().ToString(),
            LastName = Guid.NewGuid().ToString(),
            Birthday = DateTimeOffset.UtcNow,
            Street = "street",
            PostalCode = "24103",
            City = "Kiel",
            CountryCode = "DE",
            Email = email ?? "email@test.de",
            Phone = "01512345678",
            BulkMail = BulkMail.ALLOWED,
            StartOfStudies = DateTimeOffset.UtcNow,
            EndOfStudies = DateTimeOffset.UtcNow,
            AcademicDegree = AcademicDegree.BA,
            CourseOfStudy = "IT",
            TaskWithinTheClub = TaskWithinTheClub.MEMBER,
            MemberCategoryId = Guid.Parse(Program.MemberCategoriesAlumni),
            IBAN = iban ?? "IBAN",
            Bic = "DEUTDEDEXXX",
            SepaConsent = DateTimeOffset.UtcNow,
            EntryDate = DateTimeOffset.UtcNow,
            ExitDate = DateTimeOffset.UtcNow,
            ContributionPlanId = contributionPlanId
        };
    }

    private MemberUpdateRequest UpdateMemberRequest(string? email = null, string? iban = null,
        Guid? contributionPlanId = null)
    {
        return new MemberUpdateRequest()
        {
            Gender = Gender.FEMALE,
            FirstName = Guid.NewGuid().ToString(),
            MiddleName = Guid.NewGuid().ToString(),
            LastName = Guid.NewGuid().ToString(),
            Birthday = DateTimeOffset.UtcNow,
            Street = "street",
            PostalCode = "24103",
            City = "Kiel",
            CountryCode = "DE",
            Email = email ?? "email@test.de",
            Phone = "01512345678",
            BulkMail = BulkMail.NOT_ALLOWED,
            StartOfStudies = DateTimeOffset.UtcNow,
            EndOfStudies = DateTimeOffset.UtcNow,
            AcademicDegree = AcademicDegree.BA,
            CourseOfStudy = "IT",
            TaskWithinTheClub = TaskWithinTheClub.MEMBER,
            MemberCategoryId = Guid.Parse(Program.MemberCategoriesAlumni),
            IBAN = iban ?? "IBAN",
            Bic = "DEUTDEDEXXX",
            SepaConsent = DateTimeOffset.UtcNow,
            EntryDate = DateTimeOffset.UtcNow,
            ExitDate = DateTimeOffset.UtcNow,
            ContributionPlanId = contributionPlanId
        };
    }
}