using System.Net;
using System.Net.Http.Json;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Services;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using UniVerein.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

public class EnrollFormSettingsControllerTests : IntegrationTestBase
{
    public EnrollFormSettingsControllerTests(UniVereinWebApplicationFactory factory) : base(factory)
    {
    }

    public override async Task InitializeAsync()
    {
        await WithDbContext(async db =>
        {
            db.EnrollFormSettings.RemoveRange(db.EnrollFormSettings.AsQueryable());
            db.WebPageConfigs.RemoveRange(db.WebPageConfigs.AsQueryable());
            db.MailSettings.RemoveRange(db.MailSettings.AsQueryable());
            await db.ForceSaveChangesAsync();
        });
    }

    public override async Task DisposeAsync()
    {
        await WithDbContext(async db =>
        {
            db.EnrollFormSettings.RemoveRange(db.EnrollFormSettings.AsQueryable());
            db.WebPageConfigs.RemoveRange(db.WebPageConfigs.AsQueryable());
            db.MailSettings.RemoveRange(db.MailSettings.AsQueryable());
            await db.ForceSaveChangesAsync();
        });
    }

    [Fact]
    public async Task Get_WithoutToken_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/enroll-form-settings");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task GetAndPut_NonAdmin_Forbidden(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage get = await client.GetAsync("/enroll-form-settings");
        HttpResponseMessage put = await client.PutAsJsonAsync("/enroll-form-settings", new EnrollFormSettingsRequest());

        // Assert
        get.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        put.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_NothingSaved_ReturnsEmptyValues()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        EnrollFormSettingsResult? result = await client.GetFromJsonAsync<EnrollFormSettingsResult>("/enroll-form-settings");

        // Assert
        result.ShouldNotBeNull();
        result.LegalText.ShouldBeEmpty();
        result.EmbedAllowedOrigins.ShouldBeEmpty();
        result.SelfEnrollmentEnabled.ShouldBeFalse();
    }

    [Fact]
    public async Task Put_CreatesRowAndGetReturnsIt()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync("/enroll-form-settings", new EnrollFormSettingsRequest
        {
            LegalText = "<h2>Privacy</h2><p>Text</p>",
            EmbedAllowedOrigins = "https://club.example"
        });
        EnrollFormSettingsResult? loaded = await client.GetFromJsonAsync<EnrollFormSettingsResult>("/enroll-form-settings");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        loaded.ShouldNotBeNull();
        loaded.LegalText.ShouldBe("<h2>Privacy</h2><p>Text</p>");
        loaded.EmbedAllowedOrigins.ShouldBe("https://club.example");
        await WithDbContext(async db => (await db.EnrollFormSettings.CountAsync()).ShouldBe(1));
    }

    [Fact]
    public async Task Put_Twice_UpdatesTheSameRow()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        await client.PutAsJsonAsync("/enroll-form-settings", new EnrollFormSettingsRequest { LegalText = "<p>A</p>" });
        await client.PutAsJsonAsync("/enroll-form-settings", new EnrollFormSettingsRequest { LegalText = "<p>B</p>" });

        // Assert
        await WithDbContext(async db =>
        {
            List<EnrollFormSettingsEntity> rows = await db.EnrollFormSettings.ToListAsync();
            rows.Count.ShouldBe(1);
            rows[0].LegalText.ShouldBe("<p>B</p>");
        });
    }

    [Fact]
    public async Task Put_SanitizesLegalText()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync("/enroll-form-settings", new EnrollFormSettingsRequest
        {
            LegalText = "<p onclick=\"x()\">Text</p><script>alert(1)</script><iframe src=\"https://evil.example\"></iframe>"
        });
        EnrollFormSettingsResult? result = await response.Content.ReadFromJsonAsync<EnrollFormSettingsResult>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.LegalText.ShouldBe("<p>Text</p>");
    }

    [Fact]
    public async Task Put_EmptyEditorContent_StoresEmptyText()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync("/enroll-form-settings",
            new EnrollFormSettingsRequest { LegalText = "<p></p>" });
        EnrollFormSettingsResult? result = await response.Content.ReadFromJsonAsync<EnrollFormSettingsResult>();

        // Assert
        result.ShouldNotBeNull();
        result.LegalText.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("<p> </p>")]
    [InlineData("<p><br></p>")]
    [InlineData("<p>&nbsp;</p><p></p>")]
    public async Task Put_EditorContentWithoutVisibleText_StoresEmptyText(string html)
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync("/enroll-form-settings",
            new EnrollFormSettingsRequest { LegalText = html });
        EnrollFormSettingsResult? result = await response.Content.ReadFromJsonAsync<EnrollFormSettingsResult>();

        // Assert
        result.ShouldNotBeNull();
        result.LegalText.ShouldBeEmpty();
    }

    [Fact]
    public async Task Put_EnableWithoutMailSettings_UnprocessableEntity()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        await CreateWebPageConfig();

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync("/enroll-form-settings",
            new EnrollFormSettingsRequest { SelfEnrollmentEnabled = true });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await WithDbContext(async db => (await db.EnrollFormSettings.AnyAsync()).ShouldBeFalse());
    }

    [Fact]
    public async Task Put_EnableWithoutWebPageConfig_UnprocessableEntity()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        await CreateMailSettings();

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync("/enroll-form-settings",
            new EnrollFormSettingsRequest { SelfEnrollmentEnabled = true });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Put_Enable_PersistsFlagAndCapturesPublicBaseUrl()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        await CreateMailSettings();
        await CreateWebPageConfig();

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync("/enroll-form-settings",
            new EnrollFormSettingsRequest { SelfEnrollmentEnabled = true, LegalText = "<p>Text</p>" });
        EnrollFormSettingsResult? result = await response.Content.ReadFromJsonAsync<EnrollFormSettingsResult>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.SelfEnrollmentEnabled.ShouldBeTrue();
        await WithDbContext(async db =>
        {
            (await db.EnrollFormSettings.SingleAsync()).SelfEnrollmentEnabled.ShouldBeTrue();
            (await db.WebPageConfigs.SingleAsync()).PublicBaseUrl
                .ShouldBe($"{client.BaseAddress!.Scheme}://{client.BaseAddress.Host}");
        });
    }

    [Fact]
    public async Task Put_DisableWithoutMailSettings_Success()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        await WithDbContext(async db =>
        {
            await db.EnrollFormSettings.AddAsync(new EnrollFormSettingsEntity { SelfEnrollmentEnabled = true });
            await db.SaveChangesAsync();
        });

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync("/enroll-form-settings",
            new EnrollFormSettingsRequest { SelfEnrollmentEnabled = false });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await WithDbContext(async db =>
            (await db.EnrollFormSettings.SingleAsync()).SelfEnrollmentEnabled.ShouldBeFalse());
    }

    [Fact]
    public async Task Put_InvalidOriginWhileEnabling_DoesNotChangeAnything()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        await CreateMailSettings();
        await CreateWebPageConfig();

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync("/enroll-form-settings",
            new EnrollFormSettingsRequest { SelfEnrollmentEnabled = true, EmbedAllowedOrigins = "club.example" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await WithDbContext(async db =>
        {
            (await db.EnrollFormSettings.AnyAsync()).ShouldBeFalse();
            (await db.WebPageConfigs.SingleAsync()).PublicBaseUrl.ShouldBeNull();
        });
    }

    [Fact]
    public async Task Put_NormalizesOrigins()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync("/enroll-form-settings",
            new EnrollFormSettingsRequest
            {
                EmbedAllowedOrigins = " https://club.example/ \r\nhttps://CLUB.example\n\nhttps://*.verein.example:8443"
            });
        EnrollFormSettingsResult? result = await response.Content.ReadFromJsonAsync<EnrollFormSettingsResult>();

        // Assert
        result.ShouldNotBeNull();
        result.EmbedAllowedOrigins.ShouldBe("https://club.example\nhttps://*.verein.example:8443");
    }

    [Theory]
    [InlineData("club.example")]
    [InlineData("ftp://club.example")]
    [InlineData("https://club.example/path")]
    [InlineData("https://club.example; script-src *")]
    [InlineData("https://club.example https://other.example")]
    [InlineData("*")]
    [InlineData("'none'")]
    [InlineData("https://*.com")]
    [InlineData("https://*")]
    [InlineData("https://club.example:0")]
    [InlineData("https://club.example:65536")]
    public async Task Put_InvalidOrigin_BadRequest(string origin)
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync("/enroll-form-settings",
            new EnrollFormSettingsRequest { EmbedAllowedOrigins = origin });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await WithDbContext(async db => (await db.EnrollFormSettings.AnyAsync()).ShouldBeFalse());
    }

    [Fact]
    public async Task Put_TooManyOrigins_BadRequest()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        string origins = string.Join('\n', Enumerable.Range(0, EnrollFormSettingsLimits.MaxEmbedOrigins + 1)
            .Select(i => $"https://club{i}.example"));

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync("/enroll-form-settings",
            new EnrollFormSettingsRequest { EmbedAllowedOrigins = origins });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Put_LegalTextTooLong_BadRequest()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync("/enroll-form-settings",
            new EnrollFormSettingsRequest { LegalText = new string('a', EnrollFormSettingsLimits.LegalTextMaxLength + 1) });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    private async Task CreateWebPageConfig()
    {
        await WithDbContext(async db =>
        {
            await db.WebPageConfigs.AddAsync(new WebPageConfigEntity { PageName = "Club" });
            await db.SaveChangesAsync();
        });
    }

    private async Task CreateMailSettings()
    {
        CryptoService crypto = GetService<CryptoService>();
        await WithDbContext(async db =>
        {
            await db.MailSettings.AddAsync(new MailSettingsEntity
            {
                SmtpServer = "localhost",
                Port = 25,
                Username = "test",
                Password = crypto.Encrypt("test"),
                FromMail = "noreply@test.de",
                EnableSsl = false
            });
            await db.SaveChangesAsync();
        });
    }
}
