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
            await db.ForceSaveChangesAsync();
        });
    }

    public override async Task DisposeAsync()
    {
        await WithDbContext(async db =>
        {
            db.WebPageConfigs.RemoveRange(db.WebPageConfigs.AsQueryable());
            db.Members.RemoveRange(db.Members.AsQueryable());
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
        result!.MemberCategories.ShouldBeEmpty();
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
        result!.MemberCategories.ShouldBeEmpty();
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
        result!.MemberCategories.ShouldNotBeEmpty();
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

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostSelfEnrollment_Disabled_Forbidden()
    {
        // Arrange
        await CreateSelfEnrollmentConfigEntity(false);
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/self-enrollment", CreateSelfEnrollmentRequest());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PostSelfEnrollment_Enabled_CreatesMemberAsMember()
    {
        // Arrange
        await CreateSelfEnrollmentConfigEntity(true);
        HttpClient client = CreateClient();
        SelfEnrollmentRequest request = CreateSelfEnrollmentRequest();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/self-enrollment", request);
        MemberResult? result = await response.Content.ReadFromJsonAsync<MemberResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        result.ShouldNotBeNull();
        result!.TaskWithinTheClub.ShouldBe(TaskWithinTheClub.MEMBER);
        result.ExitDate.ShouldBeNull();

        await WithDbContext(async db =>
        {
            MemberEntity? member = await db.Members.FirstOrDefaultAsync(m => m.Id == result.Id);
            member.ShouldNotBeNull();
            member!.TaskWithinTheClub.ShouldBe(TaskWithinTheClub.MEMBER);
            member.ExitDate.ShouldBeNull();
        });
    }

    [Fact]
    public async Task PostSelfEnrollment_TamperedTaskWithinTheClubInRawPayload_IsIgnored()
    {
        // Arrange
        await CreateSelfEnrollmentConfigEntity(true);
        HttpClient client = CreateClient();

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
          "email": "tamper_{{Guid.NewGuid()}}@test.de",
          "bulkMail": "ALLOWED",
          "startOfStudies": "2020-01-01T00:00:00Z",
          "memberCategoryId": "{{Program.MemberCategoriesAlumni}}",
          "entryDate": "2020-01-01T00:00:00Z",
          "taskWithinTheClub": "CHAIRMAN"
        }
        """;

        // Act
        HttpResponseMessage response = await client.PostAsync("/self-enrollment",
            new StringContent(json, Encoding.UTF8, "application/json"));
        MemberResult? result = await response.Content.ReadFromJsonAsync<MemberResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        result.ShouldNotBeNull();
        result!.TaskWithinTheClub.ShouldBe(TaskWithinTheClub.MEMBER);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task PostSelfEnrollment_DuplicateEmailOrIban_Conflict(bool sameEmail, bool sameIban)
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

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
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
    public async Task PostSelfEnrollment_UnknownMemberCategory_NotFound()
    {
        // Arrange
        await CreateSelfEnrollmentConfigEntity(true);
        HttpClient client = CreateClient();
        SelfEnrollmentRequest request = CreateSelfEnrollmentRequest(memberCategoryId: Guid.NewGuid());

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/self-enrollment", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PostSelfEnrollment_UnknownContributionPlan_NotFound()
    {
        // Arrange
        await CreateSelfEnrollmentConfigEntity(true);
        HttpClient client = CreateClient();
        SelfEnrollmentRequest request = CreateSelfEnrollmentRequest(contributionPlanId: Guid.NewGuid());

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/self-enrollment", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // ---------------------------------------------------------------
    // Helper functions
    // ---------------------------------------------------------------
    private async Task<WebPageConfigEntity> CreateSelfEnrollmentConfigEntity(bool enabled)
    {
        WebPageConfigEntity config = new() { SelfEnrollmentEnabled = enabled };

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

    private SelfEnrollmentRequest CreateSelfEnrollmentRequest(string? email = null, string? iban = null,
        Guid? memberCategoryId = null, Guid? contributionPlanId = null)
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
            MemberCategoryId = memberCategoryId ?? Guid.Parse(Program.MemberCategoriesAlumni),
            IBAN = iban ?? Guid.NewGuid().ToString("N"),
            Bic = "DEUTDEDEXXX",
            EntryDate = DateTimeOffset.UtcNow,
            ContributionPlanId = contributionPlanId
        };
    }
}
