using UniVerein.Api;
using UniVerein.Api.Services;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace UniVerein.IntegrationTests.Infrastructure;

public class UniVereinWebApplicationFactory : WebApplicationFactory<Startup>, IAsyncLifetime
{
    private readonly SqliteConnection _keepAliveConnection;
    public MutableFakeTimeProvider FakeTime { get; set; } = new();

    private static readonly string TestDirectory =
        Path.GetDirectoryName(typeof(UniVereinWebApplicationFactory).Assembly.Location)!;

    public UniVereinWebApplicationFactory()
    {
        _keepAliveConnection = new SqliteConnection("DataSource=:memory:");
        _keepAliveConnection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var settingsPath = Path.Combine(TestDirectory, "appsettings.Testing.json");
            config.AddJsonFile(settingsPath, optional: false, reloadOnChange: false);
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.RemoveAll<BackupService>();
            services.RemoveAll<MailService>();
            services.AddScoped<MailService, FakeMailService>();
            services.AddScoped<BackupService, SqliteBackupService>();
            services.AddSingleton<TimeProvider>(FakeTime);

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlite(_keepAliveConnection)
                    .ReplaceService<IModelCustomizer, SqliteDateTimeOffsetCustomizer>();
            });
        });
        builder.UseEnvironment("Testing");
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.EnsureCreatedAsync();
        await SeedDatabaseAsync(db);
    }

    public new async Task DisposeAsync()
    {
        await _keepAliveConnection.DisposeAsync();
    }
    
    public async Task ResetDatabaseAsync()
    {
        using IServiceScope scope = Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        db.AuditLogs.RemoveRange(db.AuditLogs.IgnoreQueryFilters());
        db.Contributions.RemoveRange(db.Contributions.IgnoreQueryFilters());
        db.ContributionPlans.RemoveRange(db.ContributionPlans.IgnoreQueryFilters());
        db.CreditorConfigs.RemoveRange(db.CreditorConfigs.IgnoreQueryFilters());
        db.LinkSettings.RemoveRange(db.LinkSettings.IgnoreQueryFilters());
        db.MailSettings.RemoveRange(db.MailSettings.IgnoreQueryFilters());
        db.MemberCategories.RemoveRange(db.MemberCategories.IgnoreQueryFilters());
        db.Members.RemoveRange(db.Members.IgnoreQueryFilters());
        db.SepaExports.RemoveRange(db.SepaExports.IgnoreQueryFilters());
        db.Users.RemoveRange(db.Users.IgnoreQueryFilters());
        db.WebPageConfigs.RemoveRange(db.WebPageConfigs.IgnoreQueryFilters());
        await db.SaveChangesAsync();

        await SeedDatabaseAsync(db);
    }

    private static async Task SeedDatabaseAsync(AppDbContext db)
    {
        if (!db.Users.Any())
        {
            db.Users.Add(new UserEntity()
            {
                Id = Guid.NewGuid(),
                Username = "admin",
                PasswordHash = CryptoService.HashPassword("Test1234!"),
                Role = UserRole.ADMIN,
                CreatedAt = DateTimeOffset.UtcNow,
                FailedAttempts = 0,
                BlockingLoginTimeout = DateTimeOffset.UtcNow
            });

            foreach ((string id, string name) category in Program.DefaultMemberCategories)
            {
                if (!db.MemberCategories.IgnoreQueryFilters().Any(x => x.Id == Guid.Parse(category.id)))
                {
                    db.MemberCategories.Add(new MemberCategoryEntity()
                    {
                        Id = Guid.Parse(category.id),
                        Category = category.name,
                        Name = category.name
                    });
                }
            }

            await db.SaveChangesAsync();
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _keepAliveConnection.Dispose();
    }
}