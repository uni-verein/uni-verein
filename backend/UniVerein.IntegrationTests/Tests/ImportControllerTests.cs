using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Services;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using UniVerein.IntegrationTests.Infrastructure;
using Shouldly;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class ImportControllerTests : IntegrationTestBase
{
    private readonly AppDbContext _db;

    private readonly string _fileHeader =
        "Member number;Gender;Name;First name;Middle name;Birthday;Phone nummer;Mail;Bulk mail;" +
        "Street and number;ZIP code;City;Country code;Study start;Study end;Academic degree;Course of study;" +
        "Task within the club;Member category;Entry date;Exit date;IBAN;BIC;" +
        "Sepa consent date;Contribution amount";

    public ImportControllerTests(UniVereinWebApplicationFactory factory)
        : base(factory)
    {
        _db = GetService<AppDbContext>();
    }

    public override async Task InitializeAsync()
    {
        _db.Members.RemoveRange(_db.Members.AsQueryable());
        _db.ContributionPlans.RemoveRange(_db.ContributionPlans.AsQueryable());
        await _db.ForceSaveChangesAsync();
    }

    private static MultipartFormDataContent CreateCsvFormFile(string csvContent, string fileName = "test.csv")
    {
        byte[] bytes = Encoding.UTF8.GetBytes(csvContent);
        var stream = new MemoryStream(bytes);
        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        var content = new MultipartFormDataContent();
        content.Add(fileContent, "file", fileName);

        return content;
    }

    private string BuildValidCsvContent(
        int memberNumber = 1,
        string gender = "MALE",
        string name = "Mustermann",
        string firstName = "Max",
        string birthday = "01.01.2000",
        string email = "max@test.de",
        string bulkMail = "ALLOWED",
        string street = "Musterstraße 1",
        string zip = "12345",
        string city = "Musterstadt",
        string countryCode = "DE",
        string studyStart = "01.10.2015",
        string courseOfStudy = "Informatics",
        string academicDegree = "b.sc.",
        string task = "MEMBER",
        string memberCategory = "STUDENT",
        string entryDate = "2.10.2015",
        string iban = "DE89370400440532013000",
        string bic = "INGDDEFFXXX",
        string sepaConsentDate = "5.10.2015",
        int contributionAmount = 12)
    {
        string row = $"{memberNumber};{gender};{name};{firstName};;{birthday};+49172123456;{email};" +
                     $"{bulkMail};{street};{zip};{city};{countryCode};{studyStart};;{academicDegree};{courseOfStudy};" +
                     $"{task};{memberCategory};{entryDate};;{iban};{bic};{sepaConsentDate};" +
                     $"{contributionAmount}";

        return $"{_fileHeader}\n{row}";
    }

    private async Task<MemberEntity> CreateMemberAsync(string? firstName = null)
    {
        CryptoService cryptoService = GetService<CryptoService>();
        MemberEntity member = new MemberEntity
        {
            Id = Guid.NewGuid(),
            MandateId = "test_mandate",
            MemberNumber = 999,
            Gender = Gender.MALE,
            FirstName = firstName ?? "Existing",
            LastName = "Member",
            BirthdayEncrypted = cryptoService.Encrypt(DateTimeOffset.ParseExact("01.01.2000", "dd.MM.yyyy", null)),
            StreetEncrypted = cryptoService.Encrypt("Musterstraße 1"),
            PostalCode = "12345",
            City = "City",
            CountryCode = "DE",
            EmailEncrypted = cryptoService.Encrypt("max@test.de"),
            EmailHash = cryptoService.Hash("max@test.de"),
            PhoneEncrypted = cryptoService.Encrypt("+49 172 12345678"),
            BulkMail = BulkMail.ALLOWED,
            StartOfStudies = DateTimeOffset.ParseExact("15.05.2015", "dd.MM.yyyy", null),
            IBAN_Encrypted = cryptoService.Encrypt("DE89370400440532013000"),
            IBAN_Hash = cryptoService.Hash("DE89370400440532013000"),
            Bic_Encrypted = cryptoService.Encrypt("INGDDEFFXXX"),
            EntryDate = DateTimeOffset.ParseExact("15.10.2015", "dd.MM.yyyy", null),
            MemberCategoryId = Guid.Parse(Program.MemberCategoriesStudent)
        };

        await _db.Members.AddAsync(member);
        await _db.SaveChangesAsync();

        return member;
    }

    [Fact]
    public async Task UploadCsvAsync_WhenMembersExist_ThrowsBadRequestHttpException()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        await CreateMemberAsync();
        using var content = CreateCsvFormFile(BuildValidCsvContent());

        // Act
        HttpResponseMessage response = await client.PostAsync("/import/upload", content);
        ErrorDetailsResult? result = await response.Content.ReadFromJsonAsync<ErrorDetailsResult>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
        result.MoreInfo.ShouldBe("This endpoint can only be used on empty member list.");
    }

    [Fact]
    public async Task UploadCsvAsync_WhenFileIsEmpty_ThrowsBadRequestHttpException()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        using var content = CreateCsvFormFile("");

        // Act
        HttpResponseMessage response = await client.PostAsync("/import/upload", content);
        ErrorDetailsResult? result = await response.Content.ReadFromJsonAsync<ErrorDetailsResult>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
        result.MoreInfo.ShouldBe("Please upload a valid CSV-File.");
    }

    [Fact]
    public async Task UploadCsvAsync_WithValidCsv_ReturnsCorrectCount()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        using var content = CreateCsvFormFile(BuildValidCsvContent());

        // Act
        HttpResponseMessage response = await client.PostAsync("/import/upload", content);
        string result = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.ShouldBe("1");
    }

    [Fact]
    public async Task UploadCsvAsync_WithValidCsv_SavesMemberToDatabase()
    {
        // Arrange
        string firstName = Guid.NewGuid().ToString();
        string csv = BuildValidCsvContent(memberNumber: 42, firstName: firstName, name: "Mustermann");
        HttpClient client = CreateClient(UserRole.ADMIN);
        using var content = CreateCsvFormFile(csv);

        // Act
        HttpResponseMessage response = await client.PostAsync("/import/upload", content);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        MemberEntity? member = await _db.Members.FirstOrDefaultAsync(m => m.MemberNumber == 42);
        member.ShouldNotBeNull();
        member.FirstName.ShouldBe(firstName);
    }

    [Fact]
    public async Task UploadCsvAsync_WithMultipleValidRows_ReturnsCorrectCount()
    {
        // Arrange
        string row1 = "1;MALE;Mustermann;Max;;01.01.2000;+49172;max@test.de;ALLOWED;Str. 1;12345;City;DE;" +
                      "01.10.2015;;b.sc.;Informatics;MEMBER;STUDENT;10.10.2015;;DE89370400440532013000;INGDDEFFXXX;10.10.2015;12";
        string row2 = "2;FEMALE;Musterfrau;Erika;;02.02.1999;+49173;erika@test.de;NOT_ALLOWED;Str. 2;54321;Town;DE;" +
                      "01.04.2018;;m.sc.;Mathematics;MEMBER;STUDENT;10.04.2018;;DE89370400440532013001;INGDDEFFXXX;;15";
        string csvContent = $"{_fileHeader}\n{row1}\n{row2}";
        HttpClient client = CreateClient(UserRole.ADMIN);
        using var content = CreateCsvFormFile(csvContent);

        // Act
        HttpResponseMessage response = await client.PostAsync("/import/upload", content);
        string result = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.ShouldBe("2");
    }

    [Fact]
    public async Task UploadCsvAsync_WithInvalidCsvData_ThrowsBadRequestHttpException()
    {
        // Arrange
        string csvContent = "MemberNumber;Gender;Name\n;INVALID;";
        HttpClient client = CreateClient(UserRole.ADMIN);
        using var content = CreateCsvFormFile(csvContent);

        // Act
        HttpResponseMessage response = await client.PostAsync("/import/upload", content);
        ErrorDetailsResult? result = await response.Content.ReadFromJsonAsync<ErrorDetailsResult>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
        result.ErrorMessage.ShouldBe("Please upload a valid CSV-File.");
        result.MoreInfo.ShouldBe("The CSV file contains errors. No data was imported.");
        result.ErrorResultTranslation.Count.ShouldBe(11);
        List<string> translationKeys = new()
        {
            "validator.memberNumber.required", "validator.name.required", "validator.firstName.required",
            "validator.birthday.required", "validator.city.required", "validator.zip.required",
            "validator.streetAndNumber.required", "validator.entryDate.required", "validator.gender.invalid",
            "validator.bulkMail.required", "validator.mail.required"
        };
        result.ErrorResultTranslation.ForEach(x => translationKeys.Contains(x.TranslationKey).ShouldBeTrue());
    }

    [Fact]
    public async Task UploadCsvAsync_WithInvalidCsvData_ThrowsBadRequestHttpException2()
    {
        // Arrange
        string row1 = "-1;;Mustermann;Max;;01.01.2100;+49172;max@test@.de;ALLWED;Str. 1;12345;City;DE;" +
                      "01.10.2015;;b.sc..;Informatics;MEMBER_;STUDENT_;;;D89040044053201000;IGDFXXX;10.10.2015;-12";
        string csvContent = $"{_fileHeader}\n{row1}";
        HttpClient client = CreateClient(UserRole.ADMIN);
        using var content = CreateCsvFormFile(csvContent);

        // Act
        HttpResponseMessage response = await client.PostAsync("/import/upload", content);
        ErrorDetailsResult? result = await response.Content.ReadFromJsonAsync<ErrorDetailsResult>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe((int)HttpStatusCode.BadRequest);
        result.ErrorMessage.ShouldBe("Please upload a valid CSV-File.");
        result.MoreInfo.ShouldBe("The CSV file contains errors. No data was imported.");
        result.ErrorResultTranslation.Count.ShouldBe(11);
        List<string> translationKeys = new()
        {
            "validator.memberNumber.invalid", "validator.birthday.invalid", "validator.gender.required",
            "validator.entryDate.required", "validator.bulkMail.invalid", "validator.mail.invalid",
            "validator.academicDegree.invalid", "validator.taskWithinTheClub.invalid",
            "validator.iban.invalid", "validator.bic.invalid", "validator.contributionAmount.invalid"
        };
        result.ErrorResultTranslation.ForEach(x => translationKeys.Contains(x.TranslationKey).ShouldBeTrue());
    }

    [Fact]
    public async Task UploadCsvAsync_Success()
    {
        // Arrange
        CryptoService cryptoService = GetService<CryptoService>();
        string firstName = Guid.NewGuid().ToString();
        HttpClient client = CreateClient(UserRole.ADMIN);
        using var content = CreateCsvFormFile(BuildValidCsvContent(firstName: firstName));

        // Act
        HttpResponseMessage response = await client.PostAsync("/import/upload", content);
        string result = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.ShouldBe("1");
        MemberEntity? member = await _db.Members.FirstOrDefaultAsync(m => m.FirstName == firstName);
        member.ShouldNotBeNull();
        member.MandateId.Split("_")[1].ShouldBe("1");
        member.MemberNumber.ShouldBe(1);
        member.Gender.ShouldBe(Gender.MALE);
        member.FirstName.ShouldBe(firstName);
        member.LastName.ShouldBe("Mustermann");
        cryptoService.DecryptDate(member.BirthdayEncrypted).ShouldBe(DateTimeOffset.ParseExact("01.01.2000", "dd.MM.yyyy", null));
        cryptoService.Decrypt(member.StreetEncrypted).ShouldBe("Musterstraße 1");
        member.PostalCode.ShouldBe("12345");
        member.City.ShouldBe("Musterstadt");
        cryptoService.Decrypt(member.EmailEncrypted).ShouldBe("max@test.de");
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(member.EmailHash),
            Encoding.ASCII.GetBytes(cryptoService.Hash("max@test.de"))).ShouldBeTrue();
        cryptoService.Decrypt(member.PhoneEncrypted).ShouldBe("+49172123456");
        member.BulkMail.ShouldBe(BulkMail.ALLOWED);
        member.StartOfStudies.ShouldBe(DateTimeOffset.ParseExact("01.10.2015", "dd.MM.yyyy", null));
        cryptoService.Decrypt(member.IBAN_Encrypted).ShouldBe("DE89370400440532013000");
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(member.IBAN_Hash!),
            Encoding.ASCII.GetBytes(cryptoService.Hash("DE89370400440532013000"))).ShouldBeTrue();
        cryptoService.Decrypt(member.Bic_Encrypted).ShouldBe("INGDDEFFXXX");
        member.EntryDate.ShouldBe(DateTimeOffset.ParseExact("02.10.2015", "dd.MM.yyyy", null));
    }

    [Theory]
    [InlineData(BulkMail.ALLOWED)]
    [InlineData(BulkMail.NOT_ALLOWED)]
    public async Task UploadCsvAsync_WithBulkMailAllowed_SetsBulkMail(BulkMail bulkMail)
    {
        // Arrange
        string firstName = Guid.NewGuid().ToString();
        string csvContent = BuildValidCsvContent(firstName: firstName, bulkMail: bulkMail.ToString());
        HttpClient client = CreateClient(UserRole.ADMIN);
        using var content = CreateCsvFormFile(csvContent);

        // Act
        HttpResponseMessage response = await client.PostAsync("/import/upload", content);
        string result = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.ShouldBe("1");
        MemberEntity? member = await _db.Members.FirstOrDefaultAsync(m => m.FirstName == firstName);
        member.ShouldNotBeNull();
        member.BulkMail.ShouldBe(bulkMail);
    }

    [Fact]
    public async Task UploadCsvAsync_SetsSepaConsentDate()
    {
        // Arrange
        string firstName = Guid.NewGuid().ToString();
        string csvContent = BuildValidCsvContent(sepaConsentDate: "10.10.2015", firstName: firstName);
        HttpClient client = CreateClient(UserRole.ADMIN);
        using var content = CreateCsvFormFile(csvContent);

        // Act
        HttpResponseMessage response = await client.PostAsync("/import/upload", content);
        string result = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.ShouldBe("1");
        MemberEntity? member = await _db.Members.FirstOrDefaultAsync(m => m.FirstName == firstName);
        member.ShouldNotBeNull();
        member.SepaConsent.ShouldBe(DateTimeOffset.Parse("10.10.2015"));
    }

    [Fact]
    public async Task UploadCsvAsync_WithNewContributionAmount_CreatesNewContributionPlan()
    {
        // Arrange
        string firstName = Guid.NewGuid().ToString();
        string csvContent = BuildValidCsvContent(contributionAmount: 99, firstName: firstName);
        HttpClient client = CreateClient(UserRole.ADMIN);
        using var content = CreateCsvFormFile(csvContent);

        // Act
        HttpResponseMessage response = await client.PostAsync("/import/upload", content);
        string result = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        ContributionPlanEntity? plan = await _db.ContributionPlans.FirstOrDefaultAsync(p => p.Amount == 99);
        Assert.NotNull(plan);
        Assert.Equal(Interval.YEARLY, plan.Interval);
    }

    [Fact]
    public async Task UploadCsvAsync_WithExistingContributionAmount_ReusesContributionPlan()
    {
        // Arrange
        ContributionPlanEntity existingPlan = new()
        {
            Id = Guid.NewGuid(),
            Amount = 50,
            Interval = Interval.YEARLY,
            Name = "50"
        };
        _db.ContributionPlans.Add(existingPlan);
        await _db.SaveChangesAsync();
        string firstName = Guid.NewGuid().ToString();
        string csvContent = BuildValidCsvContent(contributionAmount: 50, firstName: firstName);
        HttpClient client = CreateClient(UserRole.ADMIN);
        using var content = CreateCsvFormFile(csvContent);

        // Act
        HttpResponseMessage response = await client.PostAsync("/import/upload", content);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _db.ContributionPlans.CountAsync(p => p.Amount == 50)).ShouldBe(1);
        MemberEntity? member = await _db.Members.FirstOrDefaultAsync(m => m.FirstName == firstName);
        member.ShouldNotBeNull();
        member.ContributionPlanId.ShouldNotBeNull();
        member.ContributionPlanId.ShouldBe(existingPlan.Id);
    }

    [Theory]
    [InlineData("b.sc.", AcademicDegree.BSC)]
    [InlineData("m.sc.", AcademicDegree.MSC)]
    [InlineData("ph.d.", AcademicDegree.PHD)]
    public async Task UploadCsvAsync_WithAcademicDegreeBsc_SetsBscDegree(string import, AcademicDegree academicDegree)
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        using var content = CreateCsvFormFile(BuildValidCsvContent(memberNumber: 50, academicDegree: import));

        // Act
        HttpResponseMessage response = await client.PostAsync("/import/upload", content);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        MemberEntity? member = await _db.Members.FirstOrDefaultAsync(m => m.MemberNumber == 50);
        member.ShouldNotBeNull();
        member.AcademicDegree.ShouldBe(academicDegree);
    }

    [Theory]
    [InlineData("MEMBER", TaskWithinTheClub.MEMBER)]
    [InlineData("CHAIRMAN", TaskWithinTheClub.CHAIRMAN)]
    [InlineData("SECOND_CHAIRMAN", TaskWithinTheClub.SECOND_CHAIRMAN)]
    [InlineData("JUNIOR_BOARD_MEMBER", TaskWithinTheClub.JUNIOR_BOARD_MEMBER)]
    [InlineData("CHIEF_FINANCE_OFFICER", TaskWithinTheClub.CHIEF_FINANCE_OFFICER)]
    [InlineData("WEBSITE_MANAGER", TaskWithinTheClub.WEBSITE_MANAGER)]
    [InlineData("ALUMNI_OFFICER", TaskWithinTheClub.ALUMNI_OFFICER)]
    [InlineData("STUDENT_COUNCIL_REPRESENTATIVE", TaskWithinTheClub.STUDENT_COUNCIL_REPRESENTATIVE)]
    public async Task UploadCsvAsync_WithTaskWithinClub_SetsCorrectTask(string import,
        TaskWithinTheClub taskWithinTheClub)
    {
        // Arrange
        string csv = BuildValidCsvContent(task: import, memberNumber: 60);
        HttpClient client = CreateClient(UserRole.ADMIN);
        using var content = CreateCsvFormFile(csv);

        // Act
        HttpResponseMessage response = await client.PostAsync("/import/upload", content);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        MemberEntity? member = await _db.Members.FirstOrDefaultAsync(m => m.MemberNumber == 60);
        member.ShouldNotBeNull();
        member.TaskWithinTheClub.ShouldBe(taskWithinTheClub);
    }

    [Theory]
    [InlineData("STUDENT", Program.MemberCategoriesStudent)]
    [InlineData("ALUMNI", Program.MemberCategoriesAlumni)]
    [InlineData("OTHER", Program.MemberCategoriesOther)]
    [InlineData("ALL", Program.MemberCategoriesAll)]
    [InlineData("BOARD_OF_DIRECTORS", Program.MemberCategoriesBoardOfDirectors)]
    public async Task UploadCsvAsync_WithUniversityStatus_SetsCorrectStatus(string import, string memberCategoryId)
    {
        // Arrange
        string csv = BuildValidCsvContent(memberCategory: import, memberNumber: 70);
        HttpClient client = CreateClient(UserRole.ADMIN);
        using var content = CreateCsvFormFile(csv);

        // Act
        HttpResponseMessage response = await client.PostAsync("/import/upload", content);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        MemberEntity? member = await _db.Members.FirstOrDefaultAsync(m => m.MemberNumber == 70);
        member.ShouldNotBeNull();
        member.MemberCategoryId.ShouldBe(Guid.Parse(memberCategoryId));
    }

    [Fact]
    public async Task ExampleCsvAsync_ReturnsFileResult()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.GetAsync("/import/example");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/csv");
        response.Content.Headers.ContentType?.CharSet.ShouldBe("utf-8");
        string csvContent = Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync());
        csvContent.ShouldNotBeNullOrEmpty();
        csvContent.ShouldBe("\uFEFFMember number;Gender;Name;First name;Middle name;Birthday;Phone nummer;" +
                               "Bulk mail;Mail;Street and number;ZIP code;City;Country code;Study start;Study end;Academic degree;" +
                               "Course of study;Task within the club;Member category;Entry date;Exit date;IBAN;BIC;" +
                               "Sepa consent date;Contribution amount\r\n1;MALE;Mustermann;Max;;01.01.2000;+49 172 12345678;" +
                               "ALLOWED;max.mustermann@gmail.com;Musterstraße 1;12345;Musterstadt;DE;01.10.2015;;b.sc.;" +
                               "Informatics;MEMBER;STUDENT;10.10.2015;;DE89370400440532013000;INGDDEFFXXX;10.10.2015;12\r\n");
    }

    [Fact]
    public async Task ExportCsvAsync_WithNoMembers_ReturnsEmptyFileWithHeader()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.GetAsync("/import/export");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/csv");
        response.Content.Headers.ContentType?.CharSet.ShouldBe("utf-8");
        string csvContent = Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync());
        csvContent.ShouldNotBeNullOrEmpty();
        csvContent.Replace("/^\uFEFF/", "").TrimEnd().ShouldBe(
            "\uFEFFMember number;Gender;Name;First name;Middle name;Birthday;Phone nummer;Bulk mail;" +
            "Mail;Street and number;ZIP code;City;Country code;Study start;Study end;Academic degree;Course of study;" +
            "Task within the club;Member category;Entry date;Exit date;IBAN;BIC;Sepa consent date;Contribution amount");
    }

    [Fact]
    public async Task ExportCsvAsync_WithMembers_IncludesMemberData()
    {
        // Arrange
        await CreateMemberAsync();
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.GetAsync("/import/export");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/csv");
        response.Content.Headers.ContentType?.CharSet.ShouldBe("utf-8");
        string csvContent = Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync());
        csvContent.ShouldNotBeNullOrEmpty();
        csvContent.ShouldBe("\uFEFFMember number;Gender;Name;First name;Middle name;Birthday;Phone nummer;" +
                               "Bulk mail;Mail;Street and number;ZIP code;City;Country code;Study start;Study end;Academic degree;" +
                               "Course of study;Task within the club;Member category;Entry date;Exit date;IBAN;BIC;" +
                               "Sepa consent date;Contribution amount\r\n999;MALE;Member;Existing;;01.01.2000;+49 172 12345678;" +
                               "ALLOWED;max@test.de;Musterstraße 1;12345;City;DE;15.05.2015;;;;MEMBER;STUDENT;15.10.2015;;" +
                               "DE89370400440532013000;INGDDEFFXXX;;\r\n");
    }

    [Fact]
    public async Task ExportCsvAsync_WithMultipleMembers_ExportsAllMembers()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        List<string> firstNames = new();
        for (int i = 1; i <= 3; i++)
        {
            string name = Guid.NewGuid().ToString();
            await CreateMemberAsync(firstName: name);
            firstNames.Add(name);
        }

        // Act
        HttpResponseMessage response = await client.GetAsync("/import/export");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/csv");
        response.Content.Headers.ContentType?.CharSet.ShouldBe("utf-8");
        string csvContent = Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync());
        csvContent.ShouldNotBeNullOrEmpty();
        foreach (var firstName in firstNames)
            csvContent.ShouldContain(firstName);
    }
}