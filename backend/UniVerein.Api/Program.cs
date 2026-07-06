using System;
using System.Linq;
using UniVerein.Api;
using UniVerein.Api.Services;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.Console()
    .CreateLogger();

try
{
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog((ctx, services, config) =>
    {
        config
            .ReadFrom.Configuration(ctx.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithEnvironmentName()
            .WriteTo.Console()
            .WriteTo.File(
                path: "/app/logs/uni-verein-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
            );
    });
    builder.WebHost.UseUrls("http://0.0.0.0:8095");

    Startup startup = new Startup(builder.Configuration);
    startup.ConfigureServices(builder.Services);

    WebApplication app = builder.Build();
    startup.Configure(app);

    using (IServiceScope scope = app.Services.CreateScope())
    {
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (app.Environment.IsEnvironment("Testing"))
        {
            db.Database.EnsureCreated();
        }
        else
        {
            db.Database.Migrate();
            Guid id = Guid.Parse(AdminUserId);
            if (!db.Users.IgnoreQueryFilters().Any(x => x.Id == id))
            {
                db.Users.Add(new UserEntity
                {
                    Id = id,
                    PasswordHash = CryptoService.HashPassword("admin123"),
                    Username = "Admin",
                    Role = UserRole.ADMIN
                });
                db.SaveChanges();
            }

            bool contributionPlanExists = db.ContributionPlans.Any();
            if (!contributionPlanExists)
            {
                db.ContributionPlans.Add(new ContributionPlanEntity()
                {
                    Id = Guid.Parse(ContributionPlanDefaultId),
                    Name = "Default",
                    Amount = 12,
                    Interval = Interval.YEARLY,
                });
                db.SaveChanges();
            }

            foreach ((string id, string name) category in DefaultMemberCategories)
            {
                if (!db.MemberCategories.IgnoreQueryFilters().Any(x => x.Id == Guid.Parse(category.id)))
                {
                    db.MemberCategories.Add(new MemberCategoryEntity()
                    {
                        Id = Guid.Parse(category.id),
                        Category = category.name,
                        Name = category.name
                    });
                    db.SaveChanges();
                }
            }
        }
    }

    app.UseSerilogRequestLogging();
    app.MapControllers().RequireCors("AllowFrontend");
    app.MapHub<EmailProgressHub>("/emailProgress");
    app.Run();
}
catch (Exception ex)
    when (ex is not OperationCanceledException && ex.GetType().Name != "StopTheHostException")
{
    Log.Fatal(ex, "Host terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program
{
    private const string AdminUserId = "08de9405-ed5b-4a92-84e0-c7458221447f";
    public const string ContributionPlanDefaultId = "46481ead-eb3c-4977-a3dc-805af9bd2e8c";

    public const string MemberCategoriesStudent = "bbd21be1-4d05-437f-ae76-f65b66290438";
    public const string MemberCategoriesAlumni = "67853de0-3d93-45ad-8aa4-a356441cdab9";
    public const string MemberCategoriesOther = "d0dd905b-a088-4dca-9b3a-4640ab66fadd";
    public const string MemberCategoriesAll = "73a9b489-f31d-4517-8481-a040c5c13bde";
    public const string MemberCategoriesBoardOfDirectors = "7da5c063-439b-4895-9e01-ee6e9e31d569";

    public static readonly (string id, string name)[] DefaultMemberCategories =
    [
        (MemberCategoriesStudent, "STUDENT"),
        (MemberCategoriesAlumni, "ALUMNI"),
        (MemberCategoriesOther, "OTHER"),
        (MemberCategoriesAll, "ALL"),
        (MemberCategoriesBoardOfDirectors, "BOARD_OF_DIRECTORS")
    ];
}