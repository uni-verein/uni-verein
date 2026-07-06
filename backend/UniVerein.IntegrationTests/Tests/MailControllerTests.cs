using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using UniVerein.Api.ApiRequests;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Query;
using UniVerein.Api.Services;
using UniVerein.DAL.Entities;
using UniVerein.IntegrationTests.Infrastructure;
using Shouldly;
using UniVerein.DAL.Entities.Enums;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MimeKit;
using SmtpServer;
using SmtpServer.Storage;
using xRetry;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

[Collection("NonParallelTests")]
public class MailControllerTests : IntegrationTestBase
{
    private readonly JsonSerializerOptions _jsonSerializerOptions;
    private readonly CryptoService _cryptoService;
    private SmtpServer.SmtpServer _smtpServer = null!;
    private CancellationTokenSource _cts = null!;
    private readonly List<MimeMessage> _receivedMails = new();
    private HubConnection _connection = null!;
    private TaskCompletionSource<JsonObject> _tcsProgressUpdate = null!;
    private TaskCompletionSource<JsonObject> _tcsSummary = null!;

    public MailControllerTests(UniVereinWebApplicationFactory factory) : base(factory)
    {
        _jsonSerializerOptions = new()
        {
            Converters = { new JsonStringEnumConverter() }
        };
        _cryptoService = GetService<CryptoService>();
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();

        await WithDbContext(async db =>
        {
            db.MailSettings.RemoveRange(db.MailSettings.AsQueryable());
            db.Members.RemoveRange(db.Members.AsQueryable());
            await db.ForceSaveChangesAsync();
        });

        var options = new SmtpServerOptionsBuilder()
            .ServerName("localhost")
            .Port(2520)
            .Build();

        var serviceProvider = new ServiceCollection()
            .AddSingleton<IMessageStore>(new TestMessageStore(_receivedMails))
            .BuildServiceProvider();

        _smtpServer = new SmtpServer.SmtpServer(options, serviceProvider);
        _cts = new CancellationTokenSource();
        Task startTask = _smtpServer.StartAsync(_cts.Token);
        await Task.WhenAny(startTask, Task.Delay(500));

        if (startTask.IsFaulted)
            throw new Exception("SMTP-Server not started", startTask.Exception);

        _connection = new HubConnectionBuilder()
            .WithUrl($"http://localhost/emailProgress", httpConnectionOptions =>
            {
                IConfiguration configuration = Factory.Services.GetRequiredService<IConfiguration>();
                var expiredToken = JwtTestHelper.CreateToken(
                    configuration,
                    userId: Guid.NewGuid(),
                    username: "admin",
                    role: UserRole.ADMIN,
                    lifetime: TimeSpan.FromMinutes(5));
                httpConnectionOptions.AccessTokenProvider = () => Task.FromResult<string?>(expiredToken);
                httpConnectionOptions.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
                httpConnectionOptions.Headers.Add("Authorization", $"Bearer {expiredToken}");
            })
            .Build();

        _tcsProgressUpdate = new TaskCompletionSource<JsonObject>();
        _tcsSummary = new TaskCompletionSource<JsonObject>();
        _connection.On<JsonObject>("ProgressUpdate", message => { _tcsProgressUpdate.TrySetResult(message); });
        _connection.On<JsonObject>("SendComplete", summary => { _tcsSummary.TrySetResult(summary); });

        await _connection.StartAsync();
    }

    public override async Task DisposeAsync()
    {
        await _connection.DisposeAsync();

        await WithDbContext(async db =>
        {
            db.MailSettings.RemoveRange(db.MailSettings.AsQueryable());
            db.Members.RemoveRange(db.Members.AsQueryable());
            await db.ForceSaveChangesAsync();
        });

        await _cts.CancelAsync();
    }

    // ---------------------------------------------------------------
    // Get /api/recipients
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetAllRecipients_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/mail/recipients");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task GetAllRecipients_WithExpiredToken_Unauthorized(UserRole role)
    {
        // Arrange
        IConfiguration configuration = Factory.Services.GetRequiredService<IConfiguration>();
        var expiredToken = JwtTestHelper.CreateToken(
            configuration,
            userId: Guid.NewGuid(),
            username: "expired",
            role: role,
            lifetime: TimeSpan.FromMinutes(-5));

        var client = CreateClient().WithBearerToken(expiredToken);

        // Act
        var response = await client.GetAsync("/mail/recipients");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task GetAllRecipients_Success(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);
        foreach (int index in Enumerable.Range(1, 5))
            await CreateMemberEntity($"test{index}@test.de");

        // Act
        HttpResponseMessage response = await client.GetAsync("/mail/recipients");
        AllRecipientResult? result =
            await response.Content.ReadFromJsonAsync<AllRecipientResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.Total.ShouldBe(5);
        result.Items.Count.ShouldBe(5);
        foreach (int index in Enumerable.Range(1, 5))
            result.Items.FirstOrDefault(x => x.Email == $"test{index}@test.de").ShouldNotBeNull();
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task GetAllRecipientsByCategory_Success(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);
        foreach (int index in Enumerable.Range(1, 5))
            await CreateMemberEntity($"test{index}@test.de", category: Guid.Parse(Program.MemberCategoriesStudent));
        await CreateMemberEntity($"test@test.de", category: Guid.Parse(Program.MemberCategoriesAlumni));
        RecipientQuery recipientQuery = new()
        {
            CategoryId = Guid.Parse(Program.MemberCategoriesAlumni)
        };

        // Act
        HttpResponseMessage response = await client.GetAsync($"/mail/recipients{recipientQuery.GetQueryString()}");
        AllRecipientResult? result =
            await response.Content.ReadFromJsonAsync<AllRecipientResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.Total.ShouldBe(1);
        result.Items.Count.ShouldBe(1);
        result.Items.FirstOrDefault(x => x.Email == "test@test.de").ShouldNotBeNull();
    }

    [Fact]
    public async Task GetAllRecipients_paging_Success()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.USER);
        List<MemberEntity> members = new();
        foreach (int index in Enumerable.Range(0, 5))
            members.Add(await CreateMemberEntity($"test{index}@test.de", index));

        RecipientQuery recipientQuery = new()
        {
            Offset = 1,
            Limit = 1,
        };

        // Act
        HttpResponseMessage response = await client.GetAsync($"/mail/recipients{recipientQuery.GetQueryString()}");
        AllRecipientResult? results =
            await response.Content.ReadFromJsonAsync<AllRecipientResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        results.ShouldNotBeNull();
        results.Total.ShouldBe(5);
        results.Items.Count.ShouldBe(1);
        RecipientResult? result = results.Items.FirstOrDefault();
        result.ShouldNotBeNull();
        members.First(x => x.FirstName == "1").FirstName.ShouldBe(result.FirstName);
    }

    [Theory]
    [InlineData(-1, -1)]
    [InlineData(0, 0)]
    [InlineData(10, -1)]
    public async Task GetAllRecipients_LimitOrOffset_BadRequest(int limit, int offset)
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.USER);
        RecipientQuery recipientQuery = new()
        {
            Offset = offset,
            Limit = limit
        };

        // Act
        HttpResponseMessage response = await client.GetAsync($"/mail/recipients{recipientQuery.GetQueryString()}");
        ErrorDetailsResult? results =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        results.ShouldNotBeNull();
        results.ErrorMessage.ShouldBe("Failed request validation");
        results.MoreInfo.ShouldBe("Offset and/or Limit must be greater than or equal to 1.");
    }

    // ---------------------------------------------------------------
    // Get /api/mail
    // ---------------------------------------------------------------

    [Fact]
    public async Task GetMailSetting_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/mail");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task GetMailSetting_WithExpiredToken_Unauthorized(UserRole role)
    {
        // Arrange
        IConfiguration configuration = Factory.Services.GetRequiredService<IConfiguration>();
        var expiredToken = JwtTestHelper.CreateToken(
            configuration,
            userId: Guid.NewGuid(),
            username: "expired",
            role: role,
            lifetime: TimeSpan.FromMinutes(-5));

        var client = CreateClient().WithBearerToken(expiredToken);

        // Act
        var response = await client.GetAsync("/mail");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task GetMailSetting_Forbidden(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response = await client.GetAsync("/mail");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetMailSetting_Success()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);
        MailSettingsEntity mailSetting = await CreateMailSettingsEntity();

        // Act
        HttpResponseMessage response = await client.GetAsync("/mail");
        MailSettingsResult? result =
            await response.Content.ReadFromJsonAsync<MailSettingsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.Password.ShouldBeEmpty();
        CompareMailSetting(mailSetting, result);
    }

    [Fact]
    public async Task GetMailSetting_NotFound()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.GetAsync("/mail");
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe((int)HttpStatusCode.NotFound);
        result.ErrorMessage.ShouldBe("Mail settings not found.");
    }

    // ---------------------------------------------------------------
    // CREATE /api/mail
    // ---------------------------------------------------------------

    [Fact]
    public async Task CreateMailSetting_WithoutToken_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync("/mail", CreateMailSettingRequest());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task CreateMailSetting_Forbidden(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync("/mail", CreateMailSettingRequest());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CreateMailSetting_Success()
    {
        // Arrange
        var client = CreateClient(UserRole.ADMIN);
        MailSettingsRequest request = CreateMailSettingRequest();

        // Act
        HttpResponseMessage response = await client.PutAsJsonAsync("/mail", request);
        MailSettingsResult? result =
            await response.Content.ReadFromJsonAsync<MailSettingsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.Username.ShouldBe(request.Username);
        await WithDbContext(async db =>
        {
            MailSettingsEntity? mailSettings =
                await db.MailSettings.FirstOrDefaultAsync(u => u.Username == result.Username);
            mailSettings.ShouldNotBeNull();
            CompareMailSetting(mailSettings, result);
        });
    }

    // ---------------------------------------------------------------
    // UPDATE /api/mail
    // ---------------------------------------------------------------

    [Fact]
    public async Task UpdateMailSetting_Success()
    {
        // Arrange
        var client = CreateClient(UserRole.ADMIN);
        await CreateMailSettingsEntity();
        MailSettingsRequest request = CreateMailSettingRequest();

        // Act
        var response = await client.PutAsJsonAsync($"/mail", request);
        MailSettingsResult? result =
            await response.Content.ReadFromJsonAsync<MailSettingsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.ShouldNotBeNull();
        result.Username.ShouldBe(request.Username);
        await WithDbContext(async db =>
        {
            MailSettingsEntity? mailSetting =
                await db.MailSettings.FirstOrDefaultAsync(u => u.Username == result.Username);
            mailSetting.ShouldNotBeNull();
            CompareMailSetting(mailSetting, result);
        });
    }

    // ---------------------------------------------------------------
    // DELETE /api/mail/{id}
    // ---------------------------------------------------------------

    [Fact]
    public async Task DeleteMailSettings_WithoutToken_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/mail/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task DeleteMailSettings_Forbidden(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/mail/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task DeleteMailSettings_NotFound()
    {
        // Arrange
        HttpClient client = CreateClient(UserRole.ADMIN);

        // Act
        HttpResponseMessage response = await client.DeleteAsync($"/mail/{Guid.NewGuid()}");
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe((int)HttpStatusCode.NotFound);
        result.ErrorMessage.ShouldBe("Mail setting with the given ID not found.");
    }

    [Fact]
    public async Task DeleteMailSettings_AsAdmin_ReturnsOk()
    {
        // Arrange
        HttpClient client = CreateAdminClient();
        MailSettingsEntity mailSettingsEntity = await CreateMailSettingsEntity();

        // Act
        var response = await client.DeleteAsync($"/mail/{mailSettingsEntity.Id}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ---------------------------------------------------------------
    // SEND /api/mail/test
    // ---------------------------------------------------------------

    [Fact]
    public async Task SendTestMail_WithoutToken_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync($"/mail/test", new { });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    public async Task SendTestMail_WithExpiredToken_Unauthorized(UserRole role)
    {
        // Arrange
        IConfiguration configuration = Factory.Services.GetRequiredService<IConfiguration>();
        var expiredToken = JwtTestHelper.CreateToken(
            configuration,
            userId: Guid.NewGuid(),
            username: "expired",
            role: role,
            lifetime: TimeSpan.FromMinutes(-5));

        var client = CreateClient().WithBearerToken(expiredToken);

        // Act
        var response = await client.PostAsJsonAsync("/mail/test", new { });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.ADMIN)]
    public async Task SendTestMail_ReturnsOk(UserRole role)
    {
        HttpClient client = CreateClient(role);
        MailSettingsEntity mailSetting = await CreateMailSettingsEntity(password: "test");
        await CreateMemberEntity();
        TestMailRequest request = new()
        {
            Email = "test@test.de"
        };

        // Act
        var response = await client.PostAsJsonAsync($"/mail/test", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await WaitForMailsAsync(1);
        Assert.Single(_receivedMails);
        Assert.All(_receivedMails, m => Assert.Equal($"Test mail from {mailSetting.FromMail}", m.Subject));
        Assert.All(_receivedMails, m => Assert.Equal("Email successfully configured", m.HtmlBody));
    }

    // ---------------------------------------------------------------
    // SEND /api/mail/send
    // ---------------------------------------------------------------

    [Fact]
    public async Task SendMail_WithoutToken_Unauthorized()
    {
        // Arrange
        HttpClient client = CreateClient();

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync($"/mail/send", new { });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    [InlineData(UserRole.ADMIN)]
    public async Task SendMail_MailSettings_NotFound(UserRole role)
    {
        // Arrange
        await WithDbContext(async db =>
        {
            await db.Members.AddAsync(new MemberEntity()
            {
                MandateId =  Guid.NewGuid().ToString(),
                FirstName = "John",
                LastName = "Doe",
                BirthdayEncrypted = string.Empty,
                EmailEncrypted = GetService<CryptoService>().Encrypt("test@test.de"),
                StreetEncrypted = string.Empty,
                City = string.Empty,
                PostalCode =  "1234",
                CountryCode = "DE"
            });
            await db.SaveChangesAsync();
        });
        HttpClient client = CreateClient(role);
        MailSendRequest request = new()
        {
            ConnectionId = "123456789",
            EmailData = new()
            {
                Subject = "Test",
                HtmlBody = "<h1>Test</h1>"
            },
            SelectedEmails = new()
            {
                "test@test.de"
            }
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync($"/mail/send", request);
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe((int)HttpStatusCode.NotFound);
        result.ErrorMessage.ShouldBe("Mail settings not found.");
    }

    [Theory]
    [InlineData(UserRole.USER)]
    [InlineData(UserRole.FINANCIAL_MANAGER)]
    [InlineData(UserRole.ADMIN)]
    public async Task SendMail_Member_NotFound(UserRole role)
    {
        // Arrange
        HttpClient client = CreateClient(role);
        await CreateMailSettingsEntity();
        MailSendRequest request = new()
        {
            ConnectionId = "123456789",
            EmailData = new()
            {
                Subject = "Test",
                HtmlBody = "<h1>Test</h1>"
            },
            SelectedEmails = new()
            {
                "test@test.de"
            }
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync("/mail/send", request);
        ErrorDetailsResult? result =
            await response.Content.ReadFromJsonAsync<ErrorDetailsResult>(_jsonSerializerOptions);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        result.ShouldNotBeNull();
        result.StatusCode.ShouldBe((int)HttpStatusCode.NotFound);
        result.ErrorMessage.ShouldBe("Emails could not be sent.");
    }

    [RetryTheory]
    [InlineData(UserRole.USER, null)]
    [InlineData(UserRole.FINANCIAL_MANAGER, null)]
    [InlineData(UserRole.ADMIN, null)]
    [InlineData(UserRole.USER, Program.MemberCategoriesStudent)]
    [InlineData(UserRole.FINANCIAL_MANAGER, Program.MemberCategoriesStudent)]
    [InlineData(UserRole.ADMIN, Program.MemberCategoriesStudent)]
    [InlineData(UserRole.USER, Program.MemberCategoriesAll)]
    [InlineData(UserRole.FINANCIAL_MANAGER, Program.MemberCategoriesAll)]
    [InlineData(UserRole.ADMIN, Program.MemberCategoriesAll)]
    public async Task SendMail_ReturnsOk(UserRole role, string? category)
    {
        HttpClient client = CreateClient(role);
        await CreateMailSettingsEntity(password: "test");
        await CreateMemberEntity("test@test.de", category: Guid.Parse(Program.MemberCategoriesStudent));
        await CreateMemberEntity("test2@test.de", category: Guid.Parse(Program.MemberCategoriesAlumni));
        MailSendRequest request = new()
        {
            ConnectionId = "123456789",
            EmailData = new()
            {
                Subject = "Test",
                HtmlBody = "<h1>Test</h1>"
            },
            SelectedEmails = category == null ? ["test@test.de"] : null,
            CategoryId = category != null ? Guid.Parse(category) : null
        };

        // Act
        var response = await client.PostAsJsonAsync($"/mail/send", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        if (category != Program.MemberCategoriesAll)
        {
            await WaitForMailsAsync(1);
            Assert.Single(_receivedMails);
            Assert.All(_receivedMails, m => Assert.Equal(request.EmailData.Subject, m.Subject));
            Assert.All(_receivedMails, m => Assert.Equal(request.EmailData.HtmlBody, m.HtmlBody));
        }
        else
        {
            await WaitForMailsAsync(2);
            _receivedMails.Count.ShouldBe(2);
            Assert.All(_receivedMails, m => Assert.Equal(request.EmailData.Subject, m.Subject));
            Assert.All(_receivedMails, m => Assert.Equal(request.EmailData.HtmlBody, m.HtmlBody));
        }
    }

    [Fact]
    public async Task SendMail_ExcludeDeletedAndNoMailConsent_ReturnsOk()
    {
        HttpClient client = CreateClient(UserRole.ADMIN);
        await CreateMailSettingsEntity(password: "test");
        await CreateMemberEntity("test@test.de", category: Guid.Parse(Program.MemberCategoriesStudent));
        await CreateMemberEntity("test2@test.de", category: Guid.Parse(Program.MemberCategoriesAlumni), deleted: true);
        await CreateMemberEntity("test3@test.de", category: Guid.Parse(Program.MemberCategoriesAlumni),
            bulkMail: BulkMail.NOT_ALLOWED);

        MailSendRequest request = new()
        {
            ConnectionId = "123456789",
            EmailData = new()
            {
                Subject = "Test",
                HtmlBody = "<h1>Test</h1>"
            },
            CategoryId = Guid.Parse(Program.MemberCategoriesAll)
        };

        // Act
        var response = await client.PostAsJsonAsync($"/mail/send", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await WaitForMailsAsync(1);
        Assert.Single(_receivedMails);
    }

    [Fact]
    public async Task SendMail_ServerNotConnected_NoConnection()
    {
        HttpClient client = CreateClient(UserRole.ADMIN);
        await CreateMailSettingsEntity(server: "test");
        await CreateMemberEntity("test@test.de", category: Guid.Parse(Program.MemberCategoriesStudent));

        MailSendRequest request = new()
        {
            ConnectionId = _connection.ConnectionId!,
            EmailData = new()
            {
                Subject = "Test",
                HtmlBody = "<h1>Test</h1>"
            },
            CategoryId = Guid.Parse(Program.MemberCategoriesAll)
        };

        // Act
        HttpResponseMessage response = await client.PostAsJsonAsync($"/mail/send", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        Task receivedSummery = await Task.WhenAny(_tcsSummary.Task, Task.Delay(1000));
        Assert.True(receivedSummery == _tcsSummary.Task, "Timeout");

        JsonObject resultSummary = await _tcsSummary.Task;
        Assert.Equal(1, (int)resultSummary["failed"]!);
    }

    [Fact]
    public async Task SendMail_SendBbcMailWhenRecipientCountGreaterThen150_ReturnsOk()
    {
        HttpClient client = CreateClient(UserRole.ADMIN);
        await CreateMailSettingsEntity(password: "test");
        for (int i = 0; i < 151; i++)
        {
            await CreateMemberEntity($"{i}_test@test.de", category: Guid.Parse(Program.MemberCategoriesStudent));
        }

        MailSendRequest request = new()
        {
            ConnectionId = _connection.ConnectionId!,
            EmailData = new()
            {
                Subject = "Test",
                HtmlBody = "<h1>Test</h1>"
            },
            CategoryId = Guid.Parse(Program.MemberCategoriesAll)
        };

        // Act
        var response = await client.PostAsJsonAsync($"/mail/send", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        await WaitForMailsAsync(1);
        Assert.Single(_receivedMails);

        Task received = await Task.WhenAny(_tcsSummary.Task, Task.Delay(1000));
        Assert.True(received == _tcsSummary.Task, "Timeout");
        JsonObject result = await _tcsSummary.Task;
        Assert.Equal(0, (int)result["failed"]!);

        Task receivedProgressUpdate = await Task.WhenAny(_tcsProgressUpdate.Task, Task.Delay(5000));
        Assert.True(receivedProgressUpdate == _tcsProgressUpdate.Task, "Timeout");
        JsonObject resultProgressUpdate = await _tcsProgressUpdate.Task;
        Assert.Equal(151, (int)resultProgressUpdate["total"]!);
        Assert.Equal(0, (int)resultProgressUpdate["failed"]!);
        JsonObject lastResult = (JsonObject)resultProgressUpdate["lastResult"]!;
        Assert.True((bool)lastResult["success"]!);
    }

    // ---------------------------------------------------------------
    // Helper functions
    // ---------------------------------------------------------------
    private async Task WaitForMailsAsync(int expectedCount, int timeoutMs = 15000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (_receivedMails.Count >= expectedCount)
                return;
            await Task.Delay(100);
        }

        throw new TimeoutException($"Expected {expectedCount} mail(s), but got {_receivedMails.Count}.");
    }

    private async Task<MailSettingsEntity> CreateMailSettingsEntity(string? username = null, string? password = null,
        string? server = null)
    {
        MailSettingsEntity mailEntity = new()
        {
            SmtpServer = server ?? "localhost",
            Port = 2520,
            Username = username ?? "test",
            Password = _cryptoService.Encrypt(password ?? Guid.NewGuid().ToString()),
            FromMail = "noreply@test.de",
            EnableSsl = false
        };

        await WithDbContext(async db =>
        {
            await db.MailSettings.AddAsync(mailEntity);
            await db.SaveChangesAsync();
        });

        return mailEntity;
    }

    private async Task<MemberEntity> CreateMemberEntity(string? email = null, int? firstname = null,
        Guid? category = null, BulkMail? bulkMail = null, bool? deleted = null)
    {
        MemberEntity memberEntity = new()
        {
            MandateId = Guid.NewGuid().ToString(),
            FirstName = (string)(firstname != null ? firstname.ToString() : "John")!,
            LastName = "Doe",
            EmailEncrypted = _cryptoService.Encrypt(email ?? "test@test.de"),
            BirthdayEncrypted =  _cryptoService.Encrypt(DateTime.UtcNow.AddHours(-1)),
            StreetEncrypted =  _cryptoService.Encrypt("Test"),
            City = "City",
            PostalCode =  "1234",
            CountryCode = "DE",
            MemberCategoryId = category ?? Guid.Parse(Program.MemberCategoriesStudent),
            DeletedAt = deleted == true ? DateTime.UtcNow : null,
            BulkMail = bulkMail ?? BulkMail.ALLOWED
        };

        await WithDbContext(async db =>
        {
            await db.Members.AddAsync(memberEntity);
            await db.SaveChangesAsync();
        });

        return memberEntity;
    }

    private void CompareMailSetting(MailSettingsEntity entity, MailSettingsResult result)
    {
        entity.Id.ShouldBe(result.Id);
        entity.SmtpServer.ShouldBe(result.SmtpServer);
        entity.Port.ShouldBe(result.Port);
        entity.Username.ShouldBe(result.Username);
        entity.FromMail.ShouldBe(result.FromMail);
        entity.EnableSsl.ShouldBe(result.EnableSsl);
    }

    private MailSettingsRequest CreateMailSettingRequest(string? username = null)
    {
        return new MailSettingsRequest()
        {
            SmtpServer = Guid.NewGuid().ToString(),
            Port = 2520,
            ImapServer = Guid.NewGuid().ToString(),
            ImapPort = 2520,
            Username = username ?? Guid.NewGuid().ToString(),
            Password = Guid.NewGuid().ToString(),
            FromMail = "noreply@test.de",
            EnableSsl = false
        };
    }
}