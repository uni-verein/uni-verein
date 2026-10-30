using UniVerein.Api;
using UniVerein.Api.Services;
using UniVerein.Api.Services.Calendar;
using UniVerein.Api.Security;
using UniVerein.Api.Services.Firmware;
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
using Microsoft.Extensions.Hosting;
using Xunit;

namespace UniVerein.IntegrationTests.Infrastructure;

public class UniVereinWebApplicationFactory : WebApplicationFactory<Startup>, IAsyncLifetime
{
    private readonly SqliteConnection _keepAliveConnection;
    private readonly string _connectionString;
    public MutableFakeTimeProvider FakeTime { get; set; } = new();

    private static readonly string TestDirectory =
        Path.GetDirectoryName(typeof(UniVereinWebApplicationFactory).Assembly.Location)!;

    public UniVereinWebApplicationFactory()
    {
        // A named shared-cache database gives every DbContext its own connection. With one connection shared by all of
        // them, a request still running in the background (e.g. the token validation of a SignalR poll) collides with
        // the test thread ("database is locked", "unable to delete/modify user-function due to active statements").
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = $"test-{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared
        }.ToString();
        _keepAliveConnection = new SqliteConnection(_connectionString);
        _keepAliveConnection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            string settingsPath = Path.Combine(TestDirectory, "appsettings.Testing.json");
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
            services.AddTransient<IStartupFilter, ReverseProxyPeerStartupFilter>();

            Type[] timerServices =
            [
                typeof(ContributionBackgroundService), typeof(FirmwareCheckBackgroundService),
                typeof(PendingSelfEnrollmentCleanupService), typeof(CalendarReminderBackgroundService)
            ];
            foreach (ServiceDescriptor descriptor in services
                         .Where(d => d.ServiceType == typeof(IHostedService) &&
                                     d.ImplementationType != null && timerServices.Contains(d.ImplementationType))
                         .ToList())
                services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseSqlite(_connectionString)
                    .ReplaceService<IModelCustomizer, SqliteDateTimeOffsetCustomizer>();
            });
        });
        builder.UseEnvironment("Testing");
    }

    public async Task InitializeAsync()
    {
        using IServiceScope scope = Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

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

        scope.ServiceProvider.GetRequiredService<LoginAttemptTracker>().Clear();
        scope.ServiceProvider.GetRequiredService<AccountLockoutTracker>().Clear();

        db.AuditLogs.RemoveRange(db.AuditLogs.IgnoreQueryFilters());
        db.Notifications.RemoveRange(db.Notifications.IgnoreQueryFilters());
        db.CalendarReminderLogs.RemoveRange(db.CalendarReminderLogs.IgnoreQueryFilters());
        db.CalendarFeedTokens.RemoveRange(db.CalendarFeedTokens.IgnoreQueryFilters());
        db.UserRecoveryCodes.RemoveRange(db.UserRecoveryCodes.IgnoreQueryFilters());
        db.CalendarSettings.RemoveRange(db.CalendarSettings.IgnoreQueryFilters());
        db.CalendarEventSubscriptions.RemoveRange(db.CalendarEventSubscriptions.IgnoreQueryFilters());
        db.CalendarEventParticipants.RemoveRange(db.CalendarEventParticipants.IgnoreQueryFilters());
        db.CalendarEvents.RemoveRange(db.CalendarEvents.IgnoreQueryFilters());
        db.CalendarCategories.RemoveRange(db.CalendarCategories.IgnoreQueryFilters());
        db.Contributions.RemoveRange(db.Contributions.IgnoreQueryFilters());
        db.ContributionPlans.RemoveRange(db.ContributionPlans.IgnoreQueryFilters());
        db.CreditorConfigs.RemoveRange(db.CreditorConfigs.IgnoreQueryFilters());
        db.LinkSettings.RemoveRange(db.LinkSettings.IgnoreQueryFilters());
        db.MailSettings.RemoveRange(db.MailSettings.IgnoreQueryFilters());
        db.PendingSelfEnrollments.RemoveRange(db.PendingSelfEnrollments.IgnoreQueryFilters());
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
                BlockingLoginTimeout = null
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