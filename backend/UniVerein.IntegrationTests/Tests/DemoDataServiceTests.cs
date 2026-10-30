using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using UniVerein.Api;
using UniVerein.Api.ApiResults;
using UniVerein.Api.Extensions;
using UniVerein.Api.Services;
using UniVerein.Api.Services.Demo;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using UniVerein.IntegrationTests.Infrastructure;
using Xunit;

namespace UniVerein.IntegrationTests.Tests;

[Collection("NonParallelTests")]
public class DemoDataServiceTests : IAsyncLifetime
{
    private UniVereinWebApplicationFactory _baseFactory = null!;
    private WebApplicationFactory<Startup> _demoFactory = null!;
    private string _receiptsDirectory = null!;

    public async Task InitializeAsync()
    {
        _baseFactory = new UniVereinWebApplicationFactory();
        await _baseFactory.InitializeAsync();

        _receiptsDirectory = Path.Combine(Path.GetTempPath(), $"demo-receipts-{Guid.NewGuid():N}");
        _demoFactory = _baseFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting(DemoMode.ConfigKey, "true");
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RECEIPT_STORAGE_PATH"] = _receiptsDirectory
                }));
        });
    }

    public Task DisposeAsync()
    {
        _demoFactory.Dispose();
        _baseFactory.Dispose();
        if (Directory.Exists(_receiptsDirectory))
            Directory.Delete(_receiptsDirectory, recursive: true);

        return Task.CompletedTask;
    }

    [Fact]
    public async Task ResetAsync_CreatesTheUsersShownOnTheLoginPage()
    {
        // Act
        await ResetAsync(_demoFactory);

        // Assert
        using IServiceScope scope = _demoFactory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        List<UserEntity> users = await db.Users.ToListAsync();

        users.Count.ShouldBe(3);
        AssertUser(users, DemoDataService.AdminUsername, DemoDataService.AdminPassword, UserRole.ADMIN);
        AssertUser(users, DemoDataService.UserUsername, DemoDataService.UserPassword, UserRole.USER);
        AssertUser(users, DemoDataService.FinanceUsername, DemoDataService.FinancePassword,
            UserRole.FINANCIAL_MANAGER);
    }

    [Fact]
    public async Task ResetAsync_CreatesMembersFinanceDataAndMailSettings()
    {
        // Act
        await ResetAsync(_demoFactory);

        // Assert
        using IServiceScope scope = _demoFactory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        ReceiptService receipts = scope.ServiceProvider.GetRequiredService<ReceiptService>();

        (await db.Members.CountAsync()).ShouldBe(DemoDataService.MemberCount);
        (await db.Contributions.CountAsync()).ShouldBeGreaterThan(0);
        (await db.Receipts.CountAsync()).ShouldBe(DemoDataService.ReceiptCount);
        (await db.Incomes.CountAsync()).ShouldBe(DemoDataService.IncomeCount);
        (await db.CreditorConfigs.CountAsync()).ShouldBe(1);
        (await db.WebPageConfigs.CountAsync()).ShouldBe(1);
        (await db.CalendarEvents.CountAsync()).ShouldBeGreaterThan(0);
        (await db.AuditLogs.CountAsync(a => a.Entity == nameof(MemberEntity) && a.Action == "CREATE"))
            .ShouldBe(DemoDataService.MemberCount);
        (await db.AuditLogs.CountAsync(a => a.Entity == nameof(ReceiptEntity)))
            .ShouldBe(DemoDataService.ReceiptCount);
        (await db.AuditLogs.CountAsync(a => a.Entity == nameof(IncomeEntity)))
            .ShouldBe(DemoDataService.IncomeCount);

        MailSettingsEntity mail = await db.MailSettings.SingleAsync();
        mail.SmtpServer.ShouldBe("mailpit");
        mail.EnableSsl.ShouldBeFalse();
        mail.Username.ShouldNotBeNullOrWhiteSpace();
        mail.ImapServer.ShouldNotBeNullOrWhiteSpace();
        mail.FromName.ShouldBe(DemoDataService.ClubName);

        WebPageConfigEntity pageConfig = await db.WebPageConfigs.SingleAsync();
        pageConfig.PageName.ShouldBe(DemoDataService.ClubName);
        pageConfig.Logo.ShouldStartWith("data:image/png;base64,");

        LinkSettingsEntity link = await db.LinkSettings.SingleAsync();
        link.Name.ShouldBe("Webseite");
        link.Link.ShouldBe("https://uni-verein.de");

        // The income page has the membership fees tab built in, a category of that name would show up twice.
        (await db.IncomeCategories.AnyAsync(c => c.Name == "Mitgliedsbeiträge")).ShouldBeFalse();

        List<ReceiptFileEntity> files = await db.ReceiptFiles.ToListAsync();
        files.Count.ShouldBe(DemoDataService.ReceiptCount);
        foreach (ReceiptFileEntity file in files)
        {
            string path = receipts.GetFilePath(file);
            File.Exists(path).ShouldBeTrue();
            (await File.ReadAllBytesAsync(path)).Take(4).ShouldBe("%PDF"u8.ToArray());
        }
    }

    [Fact]
    public async Task ResetAsync_CalledAgain_ReplacesEverythingAndRemovesStrayFiles()
    {
        // Arrange
        await ResetAsync(_demoFactory);
        string strayFile = Path.Combine(_receiptsDirectory, Guid.NewGuid().ToString());
        await File.WriteAllTextAsync(strayFile, "leftover");
        using (IServiceScope scope = _demoFactory.Services.CreateScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Incomes.Add(new IncomeEntity { Payer = "Visitor", Amount = 1, IncomeDate = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        // Act
        await ResetAsync(_demoFactory);

        // Assert
        using (IServiceScope scope = _demoFactory.Services.CreateScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            (await db.Incomes.CountAsync()).ShouldBe(DemoDataService.IncomeCount);
            (await db.Incomes.AnyAsync(i => i.Payer == "Visitor")).ShouldBeFalse();
            (await db.Users.CountAsync()).ShouldBe(3);
            (await db.Members.CountAsync()).ShouldBe(DemoDataService.MemberCount);
        }

        File.Exists(strayFile).ShouldBeFalse();
        Directory.GetFiles(_receiptsDirectory).Length.ShouldBe(DemoDataService.ReceiptCount);
    }

    [Fact]
    public async Task ResetAsync_WithoutDemoMode_ThrowsAndKeepsTheData()
    {
        // Arrange
        using IServiceScope scope = _baseFactory.Services.CreateScope();
        DemoDataService service = scope.ServiceProvider.GetRequiredService<DemoDataService>();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        int users = await db.Users.CountAsync();

        // Act
        await Should.ThrowAsync<InvalidOperationException>(() => service.ResetAsync());

        // Assert
        users.ShouldBeGreaterThan(0);
        (await db.Users.CountAsync()).ShouldBe(users);
    }

    [Fact]
    public async Task WebPageConfig_ReportsTheDemoFlag()
    {
        // Arrange
        await ResetAsync(_demoFactory);
        using (IServiceScope scope = _baseFactory.Services.CreateScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.WebPageConfigs.Add(new WebPageConfigEntity { PageName = "Test" });
            await db.SaveChangesAsync();
        }

        // Act
        WebPageConfigResult? demo = await _demoFactory.CreateClient()
            .GetFromJsonAsync<WebPageConfigResult>("/web-page-config");
        WebPageConfigResult? regular = await _baseFactory.CreateClient()
            .GetFromJsonAsync<WebPageConfigResult>("/web-page-config");

        // Assert
        demo.ShouldNotBeNull();
        demo.Demo.ShouldBeTrue();
        regular.ShouldNotBeNull();
        regular.Demo.ShouldBeFalse();
    }

    [Theory]
    [InlineData("2026-10-28T01:00:00Z", 2)]
    [InlineData("2026-10-28T03:00:00Z", 24)]
    [InlineData("2026-10-28T10:30:00Z", 16.5)]
    public void DelayUntilNextReset_WaitsForTheNextThreeAM(string now, double expectedHours)
    {
        TimeSpan delay = DemoResetBackgroundService.DelayUntilNextReset(DateTimeOffset.Parse(now));

        delay.TotalHours.ShouldBe(expectedHours);
    }

    private static async Task ResetAsync(WebApplicationFactory<Startup> factory)
    {
        using IServiceScope scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<DemoDataService>().ResetAsync();
    }

    private static void AssertUser(List<UserEntity> users, string username, string password, UserRole role)
    {
        UserEntity user = users.Single(u => u.Username == username);
        user.Role.ShouldBe(role);
        CryptoService.VerifyPassword(password, user.PasswordHash).ShouldBeTrue();
    }
}
