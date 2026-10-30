using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using UniVerein.Api;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Controllers;
using UniVerein.Api.Exceptions;
using UniVerein.Api.Extensions;
using UniVerein.DAL.Entities.Enums;
using UniVerein.IntegrationTests.Infrastructure;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

[Collection("NonParallelTests")]
public class DemoModeTests : IntegrationTestBase
{
    public DemoModeTests(UniVereinWebApplicationFactory factory) : base(factory)
    {
    }

    [Fact]
    public void BackupController_IsDisabledInDemo()
    {
        typeof(BackupController).GetCustomAttribute<DisabledInDemoAttribute>().ShouldNotBeNull();
    }

    [Theory]
    [InlineData("/backup")]
    [InlineData("/backup?full=true")]
    public async Task DownloadBackup_InDemoMode_ReturnsForbiddenWithDemoErrorCode(string url)
    {
        // Arrange
        using WebApplicationFactory<Startup> demoFactory = CreateDemoFactory();
        HttpClient client = CreateAdminClient(demoFactory);

        // Act
        HttpResponseMessage response = await client.GetAsync(url);
        ErrorDetailsResult? error = await response.Content.ReadFromJsonAsync<ErrorDetailsResult>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        error.ShouldNotBeNull();
        error.ErrorCode.ShouldBe(ApiErrorCodes.DEMO_MODE_DISABLED);
    }

    [Theory]
    [InlineData("backup.sql", "text/plain")]
    [InlineData("backup.zip", "application/zip")]
    public async Task Restore_InDemoMode_ReturnsForbiddenWithoutRestoring(string fileName, string contentType)
    {
        // Arrange
        using WebApplicationFactory<Startup> demoFactory = CreateDemoFactory();
        HttpClient client = CreateAdminClient(demoFactory);
        using MultipartFormDataContent content = new();
        ByteArrayContent file = new(Encoding.UTF8.GetBytes("\\! id\n"));
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        content.Add(file, "file", fileName);

        // Act
        HttpResponseMessage response = await client.PostAsync("/backup/restore", content);
        ErrorDetailsResult? error = await response.Content.ReadFromJsonAsync<ErrorDetailsResult>();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        error.ShouldNotBeNull();
        error.ErrorCode.ShouldBe(ApiErrorCodes.DEMO_MODE_DISABLED);
    }

    [Fact]
    public async Task Backup_InDemoMode_StillRequiresAuthentication()
    {
        // Arrange
        using WebApplicationFactory<Startup> demoFactory = CreateDemoFactory();
        HttpClient client = demoFactory.CreateClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/backup");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Backup_WithoutDemoMode_IsAvailableToAdmins()
    {
        // Arrange
        HttpClient client = CreateAdminClient();

        // Act
        HttpResponseMessage response = await client.GetAsync("/backup");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("1", true)]
    [InlineData("yes", true)]
    [InlineData("nonsense", true)]
    [InlineData("false", false)]
    [InlineData("FALSE", false)]
    [InlineData(" false ", false)]
    [InlineData("0", false)]
    [InlineData("no", false)]
    [InlineData("off", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("  ", false)]
    public void DemoMode_IsOffOnlyForEmptyOrExplicitFalse(string? value, bool expected)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [DemoMode.ConfigKey] = value })
            .Build();

        DemoMode.IsEnabled(configuration).ShouldBe(expected);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData(" 1 ", true)]
    [InlineData("yes", true)]
    [InlineData("on", true)]
    [InlineData("nonsense", false)]
    [InlineData("disabled", false)]
    [InlineData("n", false)]
    [InlineData("false", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void DemoMode_ResetNeedsAnExplicitValue(string? value, bool expected)
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [DemoMode.ConfigKey] = value })
            .Build();

        DemoMode.IsResetEnabled(configuration).ShouldBe(expected);
    }

    private WebApplicationFactory<Startup> CreateDemoFactory()
    {
        return Factory.WithWebHostBuilder(builder => builder.UseSetting(DemoMode.ConfigKey, "true"));
    }

    private HttpClient CreateAdminClient(WebApplicationFactory<Startup> demoFactory)
    {
        IConfiguration configuration = demoFactory.Services.GetRequiredService<IConfiguration>();
        return demoFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        }).AsAdmin(configuration, EnsureActorUser(UserRole.ADMIN));
    }
}
