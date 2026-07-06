using System.Net;
using System.Net.Http.Headers;
using UniVerein.DAL.Entities.Enums;
using UniVerein.IntegrationTests.Infrastructure;
using Shouldly;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

[Collection("NonParallelTests")]
public class BackupControllerTests : IntegrationTestBase
{
    public BackupControllerTests(UniVereinWebApplicationFactory factory) : base(factory)
    {
    }

    public override async Task InitializeAsync()
    {
        await WithDbContext(async db =>
        {
            await db.Database.ExecuteSqlRawAsync("DELETE FROM Members");
            await db.Database.ExecuteSqlRawAsync("DELETE FROM ContributionPlans");
            await db.ForceSaveChangesAsync();
        });
        await Task.CompletedTask;
    }

    public override async Task DisposeAsync()
    {
        await Task.CompletedTask;
    }

    [Fact]
    public async Task GetBackup_Unauthorized_WhenNoToken()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/backup");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task GetBackup_Forbidden_WhenNotAdmin(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response = await client.GetAsync("/backup");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetBackup_ReturnsFile_WhenAdmin()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.GetAsync("/backup");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/sql");
        response.Content.Headers.ContentDisposition?.FileNameStar.ShouldBe($"backup_{DateTime.Now:yyyyMMdd}.sql");
    }

    [Fact]
    public async Task GetBackup_ReturnsFileWithContent_WhenAdmin()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.GetAsync("/backup");
        byte[] content = await response.Content.ReadAsByteArrayAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        content.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task GetBackup_ReturnsCorrectFileName_WhenAdmin()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        string expectedFileName = $"backup_{DateTime.Now:yyyyMMdd}.sql";

        // Act

        HttpResponseMessage response = await client.GetAsync("/backup");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        ContentDispositionHeaderValue? contentDisposition = response.Content.Headers.ContentDisposition;
        contentDisposition.ShouldNotBeNull();
        contentDisposition.FileNameStar.ShouldBe(expectedFileName);
    }

    [Fact]
    public async Task RestoreAsync_Unauthorized_WhenNoToken()
    {
        // Arrange
        HttpClient client = CreateClient();
        using MultipartFormDataContent content = CreateSqlFormFile("restore.sql", "-- SQL Content");

        // Act
        HttpResponseMessage response = await client.PostAsync("/backup/restore", content);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task RestoreAsync_Forbidden_WhenNotAdmin(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);
        using MultipartFormDataContent content = CreateSqlFormFile("restore.sql", "-- SQL Content");

        // Act
        HttpResponseMessage response = await client.PostAsync("/backup/restore", content);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RestoreAsync_ReturnsOk_WhenAdminAndValidFile()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        using MultipartFormDataContent content = CreateSqlFormFile("restore.sql", "-- Valid SQL Backup");

        // Act
        HttpResponseMessage response = await client.PostAsync("/backup/restore", content);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RestoreAsync_ReturnsOk_WithLargeSqlFile()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        string largeSqlContent = string.Join("\n",
            Enumerable.Range(0, 1000).Select(i =>
                $"INSERT INTO Members (id, mandate_id, member_number, gender, first_name, middle_name, " +
                $"last_name, birthday, street, postal_code, city, country_code, emailEncrypted, " +
                $"emailHash, phone, bulk_mail, start_of_studies, end_of_studies, academic_degree, " +
                $"course_of_study, task_within_the_club, iban_encrypted, iban_hash, bic_encrypted, sepa_consent, " +
                $"entry_date, exit_date, member_category_id, contribution_plan_id, created_at, deleted_at) VALUES (" +
                $"'{Guid.NewGuid()}','20260622161849_{i}',{i},'MALE','Test','','Tester'," +
                $"'Owb3gbbvXf6zYC0R9topZM3XiiOh7KiBz2NbJy0lfdU2OhFBlksspG2x0Qd/CVieybNYNu8OcJ1/ebIPlHJVtQ=='," +
                $"'dld5g2bg5aspYWLoMkcefNPNEg852b9rD8rZAP9hDp0=','24105','Kiel',''," +
                $"'k8GWnarfE9+GXIlM2aA0yaubxESYwW7l/BYlW0bErpcTkqau2BeU1E5YJ+tUucFq','f9xTBFdODWhse8+ZciH3bUClarkWLBpJenWS1vljv30='," +
                $"'HyO+aMb0wYSgrBdyvcldhlgSiYmt6HqA6JQXPQvako4=','ALLOWED','2015-12-31 23:00:00.000000',NULL,NULL,''," +
                $"'MEMBER','fPgs6UH7Y43OF+DqQsM0j7aBvjFuuMaVYYXSaBiHoOg=','R2vO7hHBHyZIPu2L4Zpl8BsxvII87pB51v/EZTTtZnk='," +
                $"'LEpbbLyyk3jPNp+4FXx6PVKjhfgKFOBy2B/lHnnA/d4=',NULL,'2026-06-22 16:17:47.428000',NULL," +
                $"NULL,NULL,'2026-06-22 16:18:49.414336',NULL);"));
        using MultipartFormDataContent content = CreateSqlFormFile("large_backup.sql", largeSqlContent);

        // Act
        HttpResponseMessage response = await client.PostAsync("/backup/restore", content);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RestoreAsync_ReturnsOk_WithSpecificFileName()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        string fileName = $"backup_{DateTime.Now:yyyyMMdd}.sql";
        using MultipartFormDataContent content = CreateSqlFormFile(fileName, "-- SQL Backup");

        // Act
        HttpResponseMessage response = await client.PostAsync("/backup/restore", content);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RestoreAsync_ReturnsError()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        using MultipartFormDataContent content = CreateSqlFormFile("test.txt", "INSERT test");

        // Act
        HttpResponseMessage response = await client.PostAsync("/backup/restore", content);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }

    private static MultipartFormDataContent CreateSqlFormFile(string fileName, string sqlContent)
    {
        MultipartFormDataContent multipartContent = new();

        ByteArrayContent fileContent = new(System.Text.Encoding.UTF8.GetBytes(sqlContent));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/sql");

        multipartContent.Add(fileContent, "file", fileName);

        return multipartContent;
    }
}