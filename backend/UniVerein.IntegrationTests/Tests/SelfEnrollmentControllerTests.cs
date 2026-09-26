using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Services;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using UniVerein.IntegrationTests.Infrastructure;
using Shouldly;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class SelfEnrollmentControllerTests : IntegrationTestBase
{
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private readonly CryptoService _cryptoService;

    public SelfEnrollmentControllerTests(UniVereinWebApplicationFactory factory) : base(factory)
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
            db.WebPageConfigs.RemoveRange(db.WebPageConfigs.AsQueryable());
            db.Members.RemoveRange(db.Members.AsQueryable());
            db.PendingSelfEnrollments.RemoveRange(db.PendingSelfEnrollments.AsQueryable());
            await db.ForceSaveChangesAsync();
        });
    }

    public override async Task DisposeAsync()
    {
        await WithDbContext(async db =>
        {
            db.WebPageConfigs.RemoveRange(db.WebPageConfigs.AsQueryable());
            db.Members.RemoveRange(db.Members.AsQueryable());
            db.PendingSelfEnrollments.RemoveRange(db.PendingSelfEnrollments.AsQueryable());
            await db.ForceSaveChangesAsync();
        });
    }

    // ---------------------------------------------------------------
    // GET /self-enrollment/form-data
    // ---------------------------------------------------------------
    [Fact]
    public async Task GetFormData_NoConfig_ReturnsEmptyLists()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/self-enrollment/form-data");
        SelfEnrollmentFormDataResult? result =
            await response.Content.ReadFromJsonAsync<SelfEnrollmentFormDataResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.MemberCategories.ShouldBeEmpty();
        result.ContributionPlans.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetFormData_Disabled_ReturnsEmptyLists()
    {
        // Arrange
        await CreateSelfEnrollmentConfigEntity(false);
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/self-enrollment/form-data");
        SelfEnrollmentFormDataResult? result =
            await response.Content.ReadFromJsonAsync<SelfEnrollmentFormDataResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.MemberCategories.ShouldBeEmpty();
        result.ContributionPlans.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetFormData_Enabled_ReturnsCategoriesAndPlans()
    {
        // Arrange
        await CreateSelfEnrollmentConfigEntity(true);
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/self-enrollment/form-data");
        SelfEnrollmentFormDataResult? result =
            await response.Content.ReadFromJsonAsync<SelfEnrollmentFormDataResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.MemberCategories.ShouldNotBeEmpty();
        result.MemberCategories.ShouldContain(c => c.Id == Guid.Parse(Program.MemberCategoriesAlumni));
    }

    // ---------------------------------------------------------------
    // POST /self-enrollment
    // ---------------------------------------------------------------

    [Fact]
    public async Task PostSelfEnrollment_NoConfig_Forbidden()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/self-enrollment", CreateSelfEnrollmentRequest());
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        result.ShouldNotBeNull();
        result.ErrorMessage.ShouldBe("Self-enrollment is disabled.");
        result.MoreInfo.ShouldBe("Self-enrollment is currently not enabled for this club.");
    }

    [Fact]
    public async Task PostSelfEnrollment_Disabled_Forbidden()
    {
        // Arrange
        await CreateSelfEnrollmentConfigEntity(false);
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/self-enrollment", CreateSelfEnrollmentRequest());
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        result.ShouldNotBeNull();
        result.ErrorMessage.ShouldBe("Self-enrollment is disabled.");
        result.MoreInfo.ShouldBe("Self-enrollment is currently not enabled for this club.");
    }

    [Fact]
    public async Task PostSelfEnrollment_EnabledButNoPublicBaseUrlConfigured_InternalServerError()
    {
        // Arrange
        await WithDbContext(async db =>
        {
            await db.WebPageConfigs.AddAsync(new WebPageConfigEntity { SelfEnrollmentEnabled = true });
            await db.SaveChangesAsync();
        });
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/self-enrollment", CreateSelfEnrollmentRequest());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    [Fact]
    public async Task PostSelfEnrollment_Enabled_CreatesPendingRowAwaitingConfirmation_NoMemberYet()
    {
        // Arrange
        await CreateSelfEnrollmentConfigEntity(true);
        HttpClient client = CreateClient();
        SelfEnrollmentRequest request = CreateSelfEnrollmentRequest();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/self-enrollment", request);
        SelfEnrollmentSubmitResult? result =
            await response.Content.ReadFromJsonAsync<SelfEnrollmentSubmitResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        result.ShouldNotBeNull();

        string emailHash = _cryptoService.Hash(request.Email);
        await WithDbContext(async db =>
        {
            (await db.Members.AnyAsync(m => m.EmailHash == emailHash)).ShouldBeFalse();

            PendingSelfEnrollmentEntity? pending =
                await db.PendingSelfEnrollments.FirstOrDefaultAsync(x => x.EmailHash == emailHash);
            pending.ShouldNotBeNull();
            pending.Status.ShouldBe(PendingSelfEnrollmentStatus.AWAITING_EMAIL_CONFIRMATION);
            pending.ConfirmedAt.ShouldBeNull();
            pending.ConfirmationTokenExpiresAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
            pending.ConfirmationTokenHash.ShouldNotBeNullOrWhiteSpace();
        });
    }

    [Fact]
    public async Task PostSelfEnrollment_TamperedTaskWithinTheClubInRawPayload_IsIgnoredAfterApproval()
    {
        // Arrange
        await CreateSelfEnrollmentConfigEntity(true);
        HttpClient client = CreateClient();
        string email = $"tamper_{Guid.NewGuid()}@test.de";

        string json = $$"""
        {
          "gender": "MALE",
          "firstName": "Tamper",
          "lastName": "Tester",
          "birthday": "2000-01-01T00:00:00Z",
          "street": "street",
          "postalCode": "24103",
          "city": "Kiel",
          "countryCode": "DE",
          "email": "{{email}}",
          "bulkMail": "ALLOWED",
          "startOfStudies": "2020-01-01T00:00:00Z",
          "motivation": "Studiere Informatik.",
          "iban": "{{Guid.NewGuid():N}}",
          "bic": "DEUTDEDEXXX",
          "memberCategoryId": "{{Program.MemberCategoriesAlumni}}",
          "entryDate": "2020-01-01T00:00:00Z",
          "taskWithinTheClub": "CHAIRMAN"
        }
        """;

        // Act
        HttpResponseMessage response = await client.PostAsync("/self-enrollment",
            new StringContent(json, Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        string emailHash = _cryptoService.Hash(email);
        await WithDbContext(async db =>
        {
            PendingSelfEnrollmentEntity? pending =
                await db.PendingSelfEnrollments.FirstOrDefaultAsync(x => x.EmailHash == emailHash);
            pending.ShouldNotBeNull();
            pending.MemberCategoryId.ShouldBeNull();
        });
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task PostSelfEnrollment_DuplicateEmailOrIbanAgainstExistingMember_Conflict(bool sameEmail, bool sameIban)
    {
        // Arrange
        await CreateSelfEnrollmentConfigEntity(true);
        HttpClient client = CreateClient();

        string email = $"dupe_{Guid.NewGuid()}@test.de";
        string iban = $"DUPEIBAN{Guid.NewGuid():N}";
        await CreateMemberEntity(email, iban);

        SelfEnrollmentRequest request = CreateSelfEnrollmentRequest(
            email: sameEmail ? email : null,
            iban: sameIban ? iban : null);

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/self-enrollment", request);
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        result.ShouldNotBeNull();
        result!.ErrorMessage.ShouldBe("Member already exists.");
        result.MoreInfo.ShouldBe(
            "A member or a pending self-enrollment with the same email address or IBAN already exists.");
    }

    [Fact]
    public async Task PostSelfEnrollment_AlreadyAwaitingConfirmationForSameEmail_Conflict()
    {
        // Arrange
        await CreateSelfEnrollmentConfigEntity(true);
        HttpClient client = CreateClient();
        SelfEnrollmentRequest request = CreateSelfEnrollmentRequest();

        // Act
        HttpResponseMessage first = await client.PostAsJsonAsync("/self-enrollment", request);
        HttpResponseMessage second = await client.PostAsJsonAsync("/self-enrollment", request);
        ErrorDetailsResult? result =
            await second.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        result.ShouldNotBeNull();
        result!.ErrorMessage.ShouldBe("Member already exists.");
        result.MoreInfo.ShouldBe(
            "A member or a pending self-enrollment with the same email address or IBAN already exists.");
    }

    [Fact]
    public async Task PostSelfEnrollment_MissingRequiredField_BadRequest()
    {
        // Arrange
        await CreateSelfEnrollmentConfigEntity(true);
        HttpClient client = CreateClient();

        string json = """{ "firstName": "OnlyFirstName" }""";

        // Act
        HttpResponseMessage response = await client.PostAsync("/self-enrollment",
            new StringContent(json, Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostSelfEnrollment_InvalidEmailFormat_BadRequest()
    {
        // Arrange
        await CreateSelfEnrollmentConfigEntity(true);
        HttpClient client = CreateClient();
        SelfEnrollmentRequest request = CreateSelfEnrollmentRequest(email: "not-an-email");

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/self-enrollment", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostSelfEnrollment_MissingMotivation_BadRequest()
    {
        // Arrange
        await CreateSelfEnrollmentConfigEntity(true);
        HttpClient client = CreateClient();
        SelfEnrollmentRequest request = CreateSelfEnrollmentRequest(motivation: "");

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/self-enrollment", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostSelfEnrollment_MissingIban_BadRequest()
    {
        // Arrange
        await CreateSelfEnrollmentConfigEntity(true);
        HttpClient client = CreateClient();
        SelfEnrollmentRequest request = CreateSelfEnrollmentRequest(iban: "");

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/self-enrollment", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostSelfEnrollment_MissingBic_BadRequest()
    {
        // Arrange
        await CreateSelfEnrollmentConfigEntity(true);
        HttpClient client = CreateClient();
        SelfEnrollmentRequest request = CreateSelfEnrollmentRequest();
        request.Bic = "";

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/self-enrollment", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostSelfEnrollment_SendingMemberCategoryAndContributionPlanId_SilentlyIgnored()
    {
        // Arrange
        await CreateSelfEnrollmentConfigEntity(true);
        HttpClient client = CreateClient();
        Guid categoryId = Guid.Parse(Program.MemberCategoriesAlumni);
        string email = $"tamper2_{Guid.NewGuid()}@test.de";

        string json = $$"""
        {
          "gender": "MALE",
          "firstName": "Tamper",
          "lastName": "Tester",
          "birthday": "2000-01-01T00:00:00Z",
          "street": "street",
          "postalCode": "24103",
          "city": "Kiel",
          "countryCode": "DE",
          "email": "{{email}}",
          "bulkMail": "ALLOWED",
          "startOfStudies": "2020-01-01T00:00:00Z",
          "motivation": "Studiere Informatik.",
          "iban": "{{Guid.NewGuid():N}}",
          "bic": "DEUTDEDEXXX",
          "entryDate": "2020-01-01T00:00:00Z",
          "memberCategoryId": "{{categoryId}}",
          "contributionPlanId": "{{categoryId}}"
        }
        """;

        // Act
        HttpResponseMessage response = await client.PostAsync("/self-enrollment",
            new StringContent(json, Encoding.UTF8, "application/json"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        string emailHash = _cryptoService.Hash(email);
        await WithDbContext(async db =>
        {
            PendingSelfEnrollmentEntity? pending =
                await db.PendingSelfEnrollments.FirstOrDefaultAsync(x => x.EmailHash == emailHash);
            pending.ShouldNotBeNull();
            pending.MemberCategoryId.ShouldBeNull();
            pending.ContributionPlanId.ShouldBeNull();
        });
    }

    // ---------------------------------------------------------------
    // POST /self-enrollment/confirm
    // ---------------------------------------------------------------

    [Fact]
    public async Task ConfirmSelfEnrollment_ValidToken_MovesToPendingAndIsConfirmed()
    {
        // Arrange
        HttpClient client = CreateClient();
        string token = "valid-token-" + Guid.NewGuid();
        await CreatePendingSelfEnrollmentEntity(token, PendingSelfEnrollmentStatus.AWAITING_EMAIL_CONFIRMATION,
            DateTimeOffset.UtcNow.AddHours(48));

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/self-enrollment/confirm",
            new ConfirmSelfEnrollmentRequest { Token = token });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        string tokenHash = _cryptoService.Hash(token);
        await WithDbContext(async db =>
        {
            PendingSelfEnrollmentEntity? pending =
                await db.PendingSelfEnrollments.FirstOrDefaultAsync(x => x.ConfirmationTokenHash == tokenHash);
            pending.ShouldNotBeNull();
            pending.Status.ShouldBe(PendingSelfEnrollmentStatus.PENDING);
            pending.ConfirmedAt.ShouldNotBeNull();
        });
    }

    [Fact]
    public async Task ConfirmSelfEnrollment_ExpiredToken_NotFound_RowUnchanged()
    {
        // Arrange
        HttpClient client = CreateClient();
        string token = "expired-token-" + Guid.NewGuid();
        await CreatePendingSelfEnrollmentEntity(token, PendingSelfEnrollmentStatus.AWAITING_EMAIL_CONFIRMATION,
            DateTimeOffset.UtcNow.AddHours(-1));

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/self-enrollment/confirm",
            new ConfirmSelfEnrollmentRequest { Token = token });
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.ShouldNotBeNull();
        result.ErrorMessage.ShouldBe("Confirmation link invalid or expired.");
        result.MoreInfo.ShouldBe("This confirmation link is invalid, expired or was already used.");
        string tokenHash = _cryptoService.Hash(token);
        await WithDbContext(async db =>
        {
            PendingSelfEnrollmentEntity? pending =
                await db.PendingSelfEnrollments.FirstOrDefaultAsync(x => x.ConfirmationTokenHash == tokenHash);
            pending.ShouldNotBeNull();
            pending.Status.ShouldBe(PendingSelfEnrollmentStatus.AWAITING_EMAIL_CONFIRMATION);
        });
    }

    [Fact]
    public async Task ConfirmSelfEnrollment_UnknownToken_NotFound()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/self-enrollment/confirm",
            new ConfirmSelfEnrollmentRequest { Token = "does-not-exist-" + Guid.NewGuid() });
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.ShouldNotBeNull();
        result.ErrorMessage.ShouldBe("Confirmation link invalid or expired.");
        result.MoreInfo.ShouldBe("This confirmation link is invalid, expired or was already used.");
    }

    [Fact]
    public async Task ConfirmSelfEnrollment_AlreadyConfirmedToken_NotFoundOnSecondAttempt()
    {
        // Arrange
        HttpClient client = CreateClient();
        string token = "reuse-token-" + Guid.NewGuid();
        await CreatePendingSelfEnrollmentEntity(token, PendingSelfEnrollmentStatus.AWAITING_EMAIL_CONFIRMATION,
            DateTimeOffset.UtcNow.AddHours(48));

        // Act
        HttpResponseMessage first = await client.PostAsJsonAsync("/self-enrollment/confirm",
            new ConfirmSelfEnrollmentRequest { Token = token });
        HttpResponseMessage second = await client.PostAsJsonAsync("/self-enrollment/confirm",
            new ConfirmSelfEnrollmentRequest { Token = token });
        ErrorDetailsResult? result =
            await second.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        second.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.ShouldNotBeNull();
        result.ErrorMessage.ShouldBe("Confirmation link invalid or expired.");
        result.MoreInfo.ShouldBe("This confirmation link is invalid, expired or was already used.");
    }

    // ---------------------------------------------------------------
    // Helper functions
    // ---------------------------------------------------------------
    private async Task<WebPageConfigEntity> CreateSelfEnrollmentConfigEntity(bool enabled)
    {
        WebPageConfigEntity config = new()
        {
            SelfEnrollmentEnabled = enabled,
            PublicBaseUrl = enabled ? "https://club.test.invalid" : null
        };

        await WithDbContext(async db =>
        {
            await db.WebPageConfigs.AddAsync(config);
            await db.SaveChangesAsync();
        });

        return config;
    }

    private async Task<MemberEntity> CreateMemberEntity(string email, string iban)
    {
        MemberEntity member = new()
        {
            MandateId = Guid.NewGuid().ToString(),
            MemberNumber = 1,
            Gender = Gender.MALE,
            FirstName = "Existing",
            MiddleName = string.Empty,
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
            IBAN_Encrypted = _cryptoService.Encrypt(iban),
            IBAN_Hash = _cryptoService.Hash(iban),
            Bic_Encrypted = _cryptoService.Encrypt("DEUTDEDE123"),
            EntryDate = DateTimeOffset.UtcNow
        };

        await WithDbContext(async db =>
        {
            await db.Members.AddAsync(member);
            await db.SaveChangesAsync();
        });

        return member;
    }

    private async Task<PendingSelfEnrollmentEntity> CreatePendingSelfEnrollmentEntity(string token,
        PendingSelfEnrollmentStatus status, DateTimeOffset tokenExpiresAt, string? email = null)
    {
        string resolvedEmail = email ?? $"{Guid.NewGuid()}@test.de";
        PendingSelfEnrollmentEntity pending = new()
        {
            Status = status,
            ConfirmationTokenHash = _cryptoService.Hash(token),
            ConfirmationTokenExpiresAt = tokenExpiresAt,
            ConfirmedAt = status == PendingSelfEnrollmentStatus.PENDING ? DateTimeOffset.UtcNow : null,
            SubmittedIp = "127.0.0.1",
            Gender = Gender.MALE,
            FirstName = "Pending",
            MiddleName = string.Empty,
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
            MotivationEncrypted = _cryptoService.Encrypt("Studiere Informatik, möchte mich engagieren."),
            MemberCategoryId = Guid.Parse(Program.MemberCategoriesAlumni)
        };

        await WithDbContext(async db =>
        {
            await db.PendingSelfEnrollments.AddAsync(pending);
            await db.SaveChangesAsync();
        });

        return pending;
    }

    private SelfEnrollmentRequest CreateSelfEnrollmentRequest(string? email = null, string? iban = null,
        string? motivation = null)
    {
        return new SelfEnrollmentRequest()
        {
            Gender = Gender.MALE,
            FirstName = Guid.NewGuid().ToString(),
            MiddleName = string.Empty,
            LastName = Guid.NewGuid().ToString(),
            Birthday = DateTimeOffset.UtcNow,
            Street = "street",
            PostalCode = "24103",
            City = "Kiel",
            CountryCode = "DE",
            Email = email ?? $"{Guid.NewGuid()}@test.de",
            Phone = "01512345678",
            BulkMail = BulkMail.ALLOWED,
            StartOfStudies = DateTimeOffset.UtcNow,
            Motivation = motivation ?? "Studiere Informatik, möchte mich engagieren.",
            IBAN = iban ?? Guid.NewGuid().ToString("N"),
            Bic = "DEUTDEDEXXX",
            EntryDate = DateTimeOffset.UtcNow
        };
    }
}
