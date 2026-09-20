using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Models.Enums;
using UniVerein.Api.Services;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using UniVerein.IntegrationTests.Infrastructure;
using Shouldly;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class PendingSelfEnrollmentControllerTests : IntegrationTestBase
{
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private readonly CryptoService _cryptoService;

    public PendingSelfEnrollmentControllerTests(UniVereinWebApplicationFactory factory) : base(factory)
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
            db.PendingSelfEnrollments.RemoveRange(db.PendingSelfEnrollments.AsQueryable());
            db.AuditLogs.RemoveRange(db.AuditLogs.AsQueryable());
            await db.ForceSaveChangesAsync();
        });
    }

    public override async Task DisposeAsync()
    {
        await WithDbContext(async db =>
        {
            db.Members.RemoveRange(db.Members.AsQueryable());
            db.PendingSelfEnrollments.RemoveRange(db.PendingSelfEnrollments.AsQueryable());
            db.AuditLogs.RemoveRange(db.AuditLogs.AsQueryable());
            await db.ForceSaveChangesAsync();
        });
    }

    // ---------------------------------------------------------------
    // GET /pending-self-enrollments
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetAll_NoAuth_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/pending-self-enrollments");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    [InlineData(UserRole.ADMIN)]
    public async Task GetAll_AnyLoggedInRole_ReturnsOnlyConfirmedRows(UserRole role)
    {
        // Arrange
        (HttpClient client, _) = await CreateUserAndClientAsync(role, $"reviewer-{role}");
        PendingSelfEnrollmentEntity awaiting = await CreatePendingSelfEnrollmentEntity(
            PendingSelfEnrollmentStatus.AWAITING_EMAIL_CONFIRMATION);
        PendingSelfEnrollmentEntity pending = await CreatePendingSelfEnrollmentEntity(
            PendingSelfEnrollmentStatus.PENDING);

        // Act
        HttpResponseMessage response = await client.GetAsync("/pending-self-enrollments");
        PendingSelfEnrollmentResults? result =
            await response.Content.ReadFromJsonAsync<PendingSelfEnrollmentResults>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.Items.ShouldContain(x => x.Id == pending.Id);
        result.Items.ShouldNotContain(x => x.Id == awaiting.Id);
    }

    // ---------------------------------------------------------------
    // POST /pending-self-enrollments/{id}/approve
    // ---------------------------------------------------------------

    [Fact]
    public async Task Approve_UnknownId_NotFound()
    {
        // Arrange
        HttpClient client = CreateAdminClient();
        Guid unknownId = Guid.NewGuid();

        // Act
        HttpResponseMessage response = await client.PostAsync($"/pending-self-enrollments/{unknownId}/approve", null);
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.ShouldNotBeNull();
        result.ErrorMessage.ShouldBe("Pending self-enrollment not found.");
        result.MoreInfo.ShouldBe($"No pending self-enrollment with the ID {unknownId} could be found.");
    }

    [Fact]
    public async Task Approve_RowStillAwaitingConfirmation_NotFound()
    {
        // Arrange
        HttpClient client = CreateAdminClient();
        PendingSelfEnrollmentEntity awaiting = await CreatePendingSelfEnrollmentEntity(
            PendingSelfEnrollmentStatus.AWAITING_EMAIL_CONFIRMATION);

        // Act
        HttpResponseMessage response = await client.PostAsync($"/pending-self-enrollments/{awaiting.Id}/approve", null);
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.ShouldNotBeNull();
        result.ErrorMessage.ShouldBe("Pending self-enrollment not found.");
        result.MoreInfo.ShouldBe($"No pending self-enrollment with the ID {awaiting.Id} could be found.");
    }

    [Fact]
    public async Task Approve_HappyPath_CreatesMemberAndHardDeletesPendingRow()
    {
        // Arrange
        (HttpClient client, _) = await CreateUserAndClientAsync(UserRole.USER, "reviewer");
        string email = $"approve_{Guid.NewGuid()}@test.de";
        PendingSelfEnrollmentEntity pending = await CreatePendingSelfEnrollmentEntity(
            PendingSelfEnrollmentStatus.PENDING, email: email);

        // Act
        HttpResponseMessage response = await client.PostAsync($"/pending-self-enrollments/{pending.Id}/approve", null);
        MemberResult? result = await response.Content.ReadFromJsonAsync<MemberResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        result.ShouldNotBeNull();
        result.TaskWithinTheClub.ShouldBe(TaskWithinTheClub.MEMBER);
        result.Email.ShouldBe(email);

        await WithDbContext(async db =>
        {
            (await db.PendingSelfEnrollments.AnyAsync(x => x.Id == pending.Id)).ShouldBeFalse();

            MemberEntity? member = await db.Members.FirstOrDefaultAsync(m => m.Id == result.Id);
            member.ShouldNotBeNull();
            member.TaskWithinTheClub.ShouldBe(TaskWithinTheClub.MEMBER);
        });
    }

    [Fact]
    public async Task Approve_DuplicateEmailAgainstExistingMember_ConflictAndRowStaysPending()
    {
        // Arrange
        HttpClient client = CreateAdminClient();
        string email = $"dupe_{Guid.NewGuid()}@test.de";
        await CreateMemberEntity(email);
        PendingSelfEnrollmentEntity pending = await CreatePendingSelfEnrollmentEntity(
            PendingSelfEnrollmentStatus.PENDING, email: email);

        // Act
        HttpResponseMessage response = await client.PostAsync($"/pending-self-enrollments/{pending.Id}/approve", null);
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        result.ShouldNotBeNull();
        result!.ErrorMessage.ShouldBe("Member already exists.");
        result.MoreInfo.ShouldBe("A member with the same email address or IBAN already exists.");
        await WithDbContext(async db =>
        {
            PendingSelfEnrollmentEntity? stillPending =
                await db.PendingSelfEnrollments.FirstOrDefaultAsync(x => x.Id == pending.Id);
            stillPending.ShouldNotBeNull();
            stillPending.Status.ShouldBe(PendingSelfEnrollmentStatus.PENDING);
        });
    }

    [Fact]
    public async Task Approve_MemberCategoryDeletedSinceSubmission_NotFoundAndRowStaysPending()
    {
        // Arrange
        HttpClient client = CreateAdminClient();
        Guid categoryId = await CreateMemberCategoryEntity();
        PendingSelfEnrollmentEntity pending =
            await CreatePendingSelfEnrollmentEntity(PendingSelfEnrollmentStatus.PENDING, memberCategoryId: categoryId);

        await WithDbContext(async db =>
        {
            MemberCategoryEntity category = await db.MemberCategories.FirstAsync(x => x.Id == categoryId);
            category.DeletedAt = DateTimeOffset.UtcNow;
            db.Update(category);
            await db.SaveChangesAsync();
        });

        // Act
        HttpResponseMessage response = await client.PostAsync($"/pending-self-enrollments/{pending.Id}/approve", null);
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.ShouldNotBeNull();
        result!.ErrorMessage.ShouldBe("Member category not found.");
        result.MoreInfo.ShouldBe("The member category referenced by this submission no longer exists.");
        await WithDbContext(async db =>
        {
            (await db.PendingSelfEnrollments.AnyAsync(x => x.Id == pending.Id)).ShouldBeTrue();
        });
    }

    [Fact]
    public async Task Approve_MemberCategoryNotSet_UnprocessableEntityAndRowStaysPending()
    {
        // Arrange
        HttpClient client = CreateAdminClient();
        PendingSelfEnrollmentEntity pending = await CreatePendingSelfEnrollmentEntity(
            PendingSelfEnrollmentStatus.PENDING, assignCategory: false);

        // Act
        HttpResponseMessage response = await client.PostAsync($"/pending-self-enrollments/{pending.Id}/approve", null);
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        result.ShouldNotBeNull();
        result.ErrorMessage.ShouldBe("Member category not set.");
        result.MoreInfo.ShouldBe("This submission has no assigned member category yet. Assign one before approving.");
        await WithDbContext(async db =>
        {
            PendingSelfEnrollmentEntity? stillPending =
                await db.PendingSelfEnrollments.FirstOrDefaultAsync(x => x.Id == pending.Id);
            stillPending.ShouldNotBeNull();
            stillPending.Status.ShouldBe(PendingSelfEnrollmentStatus.PENDING);
        });
    }

    // ---------------------------------------------------------------
    // POST /pending-self-enrollments/{id}/reject
    // ---------------------------------------------------------------

    [Fact]
    public async Task Reject_UnknownId_NotFound()
    {
        // Arrange
        HttpClient client = CreateAdminClient();
        Guid unknownId = Guid.NewGuid();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync($"/pending-self-enrollments/{unknownId}/reject",
            new RejectPendingSelfEnrollmentRequest());
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.ShouldNotBeNull();
        result.ErrorMessage.ShouldBe("Pending self-enrollment not found.");
        result.MoreInfo.ShouldBe($"No pending self-enrollment with the ID {unknownId} could be found.");
    }

    [Fact]
    public async Task Reject_RowStillAwaitingConfirmation_NotFound()
    {
        // Arrange
        HttpClient client = CreateAdminClient();
        PendingSelfEnrollmentEntity awaiting = await CreatePendingSelfEnrollmentEntity(
            PendingSelfEnrollmentStatus.AWAITING_EMAIL_CONFIRMATION);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync($"/pending-self-enrollments/{awaiting.Id}/reject",
            new RejectPendingSelfEnrollmentRequest());
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.ShouldNotBeNull();
        result.ErrorMessage.ShouldBe("Pending self-enrollment not found.");
        result.MoreInfo.ShouldBe($"No pending self-enrollment with the ID {awaiting.Id} could be found.");
    }

    [Fact]
    public async Task Reject_HappyPath_HardDeletesRowAndWritesAuditLog()
    {
        // Arrange
        (HttpClient client, Guid reviewerId) = await CreateUserAndClientAsync(UserRole.USER, "rejector");
        PendingSelfEnrollmentEntity pending = await CreatePendingSelfEnrollmentEntity(PendingSelfEnrollmentStatus.PENDING);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync($"/pending-self-enrollments/{pending.Id}/reject",
            new RejectPendingSelfEnrollmentRequest { Reason = "Looks like spam" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await WithDbContext(async db =>
        {
            (await db.PendingSelfEnrollments.AnyAsync(x => x.Id == pending.Id)).ShouldBeFalse();

            AuditLogEntity? auditLog = await db.AuditLogs
                .FirstOrDefaultAsync(x => x.Entity == nameof(PendingSelfEnrollmentEntity) && x.UserId == reviewerId);
            auditLog.ShouldNotBeNull();
            auditLog.Action.ShouldBe(nameof(AuditLogActions.DELETE));
            auditLog.Data.ShouldContain("Looks like spam");
        });
    }

    // ---------------------------------------------------------------
    // GET /pending-self-enrollments/{id}
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetById_NoAuth_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync($"/pending-self-enrollments/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetById_UnknownId_NotFound()
    {
        // Arrange
        HttpClient client = CreateAdminClient();
        Guid unknownId = Guid.NewGuid();

        // Act
        HttpResponseMessage response = await client.GetAsync($"/pending-self-enrollments/{unknownId}");
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.ShouldNotBeNull();
        result.ErrorMessage.ShouldBe("Pending self-enrollment not found.");
        result.MoreInfo.ShouldBe($"No pending self-enrollment with the ID {unknownId} could be found.");
    }

    [Fact]
    public async Task GetById_RowStillAwaitingConfirmation_NotFound()
    {
        // Arrange
        HttpClient client = CreateAdminClient();
        PendingSelfEnrollmentEntity awaiting = await CreatePendingSelfEnrollmentEntity(
            PendingSelfEnrollmentStatus.AWAITING_EMAIL_CONFIRMATION);

        // Act
        HttpResponseMessage response = await client.GetAsync($"/pending-self-enrollments/{awaiting.Id}");
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.ShouldNotBeNull();
        result.ErrorMessage.ShouldBe("Pending self-enrollment not found.");
        result.MoreInfo.ShouldBe($"No pending self-enrollment with the ID {awaiting.Id} could be found.");
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    [InlineData(UserRole.ADMIN)]
    public async Task GetById_AnyLoggedInRole_ReturnsFullDecryptedFields(UserRole role)
    {
        // Arrange
        (HttpClient client, _) = await CreateUserAndClientAsync(role, $"viewer-{role}");
        string email = $"detail_{Guid.NewGuid()}@test.de";
        PendingSelfEnrollmentEntity pending = await CreatePendingSelfEnrollmentEntity(
            PendingSelfEnrollmentStatus.PENDING, email: email);

        // Act
        HttpResponseMessage response = await client.GetAsync($"/pending-self-enrollments/{pending.Id}");
        PendingSelfEnrollmentDetailResult? result =
            await response.Content.ReadFromJsonAsync<PendingSelfEnrollmentDetailResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.Id.ShouldBe(pending.Id);
        result.Email.ShouldBe(email);
        result.FirstName.ShouldBe("Pending");
        result.LastName.ShouldBe("Applicant");
        result.City.ShouldBe("Kiel");
    }

    // ---------------------------------------------------------------
    // PATCH /pending-self-enrollments/{id}
    // ---------------------------------------------------------------

    [Fact]
    public async Task Update_UnknownId_NotFound()
    {
        // Arrange
        HttpClient client = CreateAdminClient();
        Guid unknownId = Guid.NewGuid();

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync($"/pending-self-enrollments/{unknownId}",
            new PendingSelfEnrollmentUpdateRequest { FirstName = "Changed" });
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.ShouldNotBeNull();
        result.ErrorMessage.ShouldBe("Pending self-enrollment not found.");
        result.MoreInfo.ShouldBe($"No pending self-enrollment with the ID {unknownId} could be found.");
    }

    [Fact]
    public async Task Update_RowStillAwaitingConfirmation_NotFound()
    {
        // Arrange
        HttpClient client = CreateAdminClient();
        PendingSelfEnrollmentEntity awaiting = await CreatePendingSelfEnrollmentEntity(
            PendingSelfEnrollmentStatus.AWAITING_EMAIL_CONFIRMATION);

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync($"/pending-self-enrollments/{awaiting.Id}",
            new PendingSelfEnrollmentUpdateRequest { FirstName = "Changed" });
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.ShouldNotBeNull();
        result.ErrorMessage.ShouldBe("Pending self-enrollment not found.");
        result.MoreInfo.ShouldBe($"No pending self-enrollment with the ID {awaiting.Id} could be found.");
    }

    [Fact]
    public async Task Update_HappyPath_ChangesReflectedAndAuditLogWritten()
    {
        // Arrange
        (HttpClient client, Guid reviewerId) = await CreateUserAndClientAsync(UserRole.USER, "editor");
        PendingSelfEnrollmentEntity pending = await CreatePendingSelfEnrollmentEntity(PendingSelfEnrollmentStatus.PENDING);
        Guid newCategoryId = await CreateMemberCategoryEntity();

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync($"/pending-self-enrollments/{pending.Id}",
            new PendingSelfEnrollmentUpdateRequest { MemberCategoryId = newCategoryId, FirstName = "Corrected" });
        PendingSelfEnrollmentDetailResult? result =
            await response.Content.ReadFromJsonAsync<PendingSelfEnrollmentDetailResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.MemberCategoryId.ShouldBe(newCategoryId);
        result.FirstName.ShouldBe("Corrected");

        await WithDbContext(async db =>
        {
            PendingSelfEnrollmentEntity? updated =
                await db.PendingSelfEnrollments.FirstOrDefaultAsync(x => x.Id == pending.Id);
            updated.ShouldNotBeNull();
            updated.Status.ShouldBe(PendingSelfEnrollmentStatus.PENDING);
            updated.MemberCategoryId.ShouldBe(newCategoryId);
            updated.FirstName.ShouldBe("Corrected");

            AuditLogEntity? auditLog = await db.AuditLogs
                .FirstOrDefaultAsync(x => x.Entity == nameof(PendingSelfEnrollmentEntity) && x.UserId == reviewerId);
            auditLog.ShouldNotBeNull();
            auditLog.Action.ShouldBe(nameof(AuditLogActions.UPDATE));
        });
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    [InlineData(UserRole.ADMIN)]
    public async Task Update_AnyLoggedInRole_Succeeds(UserRole role)
    {
        // Arrange
        (HttpClient client, _) = await CreateUserAndClientAsync(role, $"editor-{role}");
        PendingSelfEnrollmentEntity pending = await CreatePendingSelfEnrollmentEntity(PendingSelfEnrollmentStatus.PENDING);

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync($"/pending-self-enrollments/{pending.Id}",
            new PendingSelfEnrollmentUpdateRequest { LastName = "Updated" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Update_UnknownMemberCategory_NotFound()
    {
        // Arrange
        HttpClient client = CreateAdminClient();
        PendingSelfEnrollmentEntity pending = await CreatePendingSelfEnrollmentEntity(PendingSelfEnrollmentStatus.PENDING);
        Guid unknownCategoryId = Guid.NewGuid();

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync($"/pending-self-enrollments/{pending.Id}",
            new PendingSelfEnrollmentUpdateRequest { MemberCategoryId = unknownCategoryId });
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.ShouldNotBeNull();
        result.ErrorMessage.ShouldBe("Member category not found.");
        result.MoreInfo.ShouldBe($"No member category found with ID {unknownCategoryId}.");
    }

    [Fact]
    public async Task Update_UnknownContributionPlan_NotFound()
    {
        // Arrange
        HttpClient client = CreateAdminClient();
        PendingSelfEnrollmentEntity pending = await CreatePendingSelfEnrollmentEntity(PendingSelfEnrollmentStatus.PENDING);
        Guid unknownPlanId = Guid.NewGuid();

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync($"/pending-self-enrollments/{pending.Id}",
            new PendingSelfEnrollmentUpdateRequest { ContributionPlanId = unknownPlanId });
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.ShouldNotBeNull();
        result.ErrorMessage.ShouldBe("Contribution plan not found.");
        result.MoreInfo.ShouldBe($"No contribution plan found with ID {unknownPlanId}.");
    }

    [Fact]
    public async Task Update_EmailDuplicateAgainstExistingMember_Conflict()
    {
        // Arrange
        HttpClient client = CreateAdminClient();
        string existingEmail = $"existing_{Guid.NewGuid()}@test.de";
        await CreateMemberEntity(existingEmail);
        PendingSelfEnrollmentEntity pending = await CreatePendingSelfEnrollmentEntity(PendingSelfEnrollmentStatus.PENDING);

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync($"/pending-self-enrollments/{pending.Id}",
            new PendingSelfEnrollmentUpdateRequest { Email = existingEmail });
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        result.ShouldNotBeNull();
        result.ErrorMessage.ShouldBe("Member already exists.");
        result.MoreInfo.ShouldBe("A member with the same email address or IBAN already exists.");
        await WithDbContext(async db =>
        {
            PendingSelfEnrollmentEntity? stillPending =
                await db.PendingSelfEnrollments.FirstOrDefaultAsync(x => x.Id == pending.Id);
            stillPending.ShouldNotBeNull();
            stillPending.EmailHash.ShouldBe(pending.EmailHash);
        });
    }

    [Fact]
    public async Task Update_EmailDuplicateAgainstOtherPendingRow_Conflict()
    {
        // Arrange
        HttpClient client = CreateAdminClient();
        string otherEmail = $"other_{Guid.NewGuid()}@test.de";
        await CreatePendingSelfEnrollmentEntity(PendingSelfEnrollmentStatus.PENDING, email: otherEmail);
        PendingSelfEnrollmentEntity pending = await CreatePendingSelfEnrollmentEntity(PendingSelfEnrollmentStatus.PENDING);

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync($"/pending-self-enrollments/{pending.Id}",
            new PendingSelfEnrollmentUpdateRequest { Email = otherEmail });
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        result.ShouldNotBeNull();
        result!.ErrorMessage.ShouldBe("Member already exists.");
        result.MoreInfo.ShouldBe("A member with the same email address or IBAN already exists.");
    }

    [Fact]
    public async Task Update_ThenApprove_MemberReflectsCorrectedData()
    {
        // Arrange
        HttpClient client = CreateAdminClient();
        PendingSelfEnrollmentEntity pending = await CreatePendingSelfEnrollmentEntity(
            PendingSelfEnrollmentStatus.PENDING, assignCategory: false);
        Guid assignedCategoryId = await CreateMemberCategoryEntity();

        // Act
        HttpResponseMessage updateResponse = await client.PatchAsJsonAsync($"/pending-self-enrollments/{pending.Id}",
            new PendingSelfEnrollmentUpdateRequest { MemberCategoryId = assignedCategoryId });
        updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        HttpResponseMessage approveResponse = await client.PostAsync($"/pending-self-enrollments/{pending.Id}/approve", null);
        MemberResult? member = await approveResponse.Content.ReadFromJsonAsync<MemberResult>(_jsonSerializerOptions);

        // Assert
        approveResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        member.ShouldNotBeNull();
        member.MemberCategoryId.ShouldBe(assignedCategoryId);
    }

    [Fact]
    public async Task Update_SetsMotivation_MotivationEncryptedAndReturnedInDetail()
    {
        // Arrange
        string motivation = "New reason";
        HttpClient client = CreateAdminClient();
        PendingSelfEnrollmentEntity pending = await CreatePendingSelfEnrollmentEntity(PendingSelfEnrollmentStatus.PENDING);

        // Act
        HttpResponseMessage response = await client.PatchAsJsonAsync($"/pending-self-enrollments/{pending.Id}",
            new PendingSelfEnrollmentUpdateRequest { Motivation = motivation });
        PendingSelfEnrollmentDetailResult? result =
            await response.Content.ReadFromJsonAsync<PendingSelfEnrollmentDetailResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.Motivation.ShouldBe(motivation);

        await WithDbContext(async db =>
        {
            PendingSelfEnrollmentEntity? updated =
                await db.PendingSelfEnrollments.FirstOrDefaultAsync(x => x.Id == pending.Id);
            updated.ShouldNotBeNull();
            _cryptoService.Decrypt(updated.MotivationEncrypted).ShouldBe(motivation);
        });
    }

    // ---------------------------------------------------------------
    // Helper functions
    // ---------------------------------------------------------------
    private async Task<Guid> CreateMemberCategoryEntity()
    {
        MemberCategoryEntity category = new() { Category = "TEST", Name = "Test category" };
        await WithDbContext(async db =>
        {
            await db.MemberCategories.AddAsync(category);
            await db.SaveChangesAsync();
        });

        return category.Id;
    }

    private async Task CreateMemberEntity(string email)
    {
        MemberEntity member = new()
        {
            MandateId = Guid.NewGuid().ToString(),
            MemberNumber = 1,
            Gender = Gender.MALE,
            FirstName = "Existing",
            LastName = "Member",
            BirthdayEncrypted = _cryptoService.Encrypt(DateTimeOffset.UtcNow),
            StreetEncrypted = _cryptoService.Encrypt("street"),
            PostalCode = "24103",
            City = "Kiel",
            CountryCode = "DE",
            EmailEncrypted = _cryptoService.Encrypt(email),
            EmailHash = _cryptoService.Hash(email),
            PhoneEncrypted = _cryptoService.Encrypt("01512345678"),
            BulkMail = BulkMail.ALLOWED,
            StartOfStudies = DateTimeOffset.UtcNow,
            TaskWithinTheClub = TaskWithinTheClub.MEMBER,
            MemberCategoryId = Guid.Parse(Program.MemberCategoriesAlumni),
            EntryDate = DateTimeOffset.UtcNow
        };

        await WithDbContext(async db =>
        {
            await db.Members.AddAsync(member);
            await db.SaveChangesAsync();
        });
    }

    private async Task<PendingSelfEnrollmentEntity> CreatePendingSelfEnrollmentEntity(
        PendingSelfEnrollmentStatus status, string? email = null, Guid? memberCategoryId = null,
        bool assignCategory = true)
    {
        string resolvedEmail = email ?? $"{Guid.NewGuid()}@test.de";
        PendingSelfEnrollmentEntity pending = new()
        {
            Status = status,
            ConfirmationTokenHash = _cryptoService.Hash(Guid.NewGuid().ToString()),
            ConfirmationTokenExpiresAt = DateTimeOffset.UtcNow.AddHours(48),
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
            EmailEncrypted = _cryptoService.Encrypt(resolvedEmail),
            EmailHash = _cryptoService.Hash(resolvedEmail),
            PhoneEncrypted = _cryptoService.Encrypt("01512345678"),
            BulkMail = BulkMail.ALLOWED,
            StartOfStudies = DateTimeOffset.UtcNow,
            MotivationEncrypted = _cryptoService.Encrypt("Studiere Informatik."),
            MemberCategoryId = assignCategory ? (memberCategoryId ?? Guid.Parse(Program.MemberCategoriesAlumni)) : null
        };

        await WithDbContext(async db =>
        {
            await db.PendingSelfEnrollments.AddAsync(pending);
            await db.SaveChangesAsync();
        });

        return pending;
    }
}
