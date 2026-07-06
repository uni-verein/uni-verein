using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using UniVerein.DAL.Entities;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace UniVerein.DAL.Data;

public class AppDbContext : DbContext
{
    private readonly TimeProvider _timeProvider;

    public AppDbContext(DbContextOptions<AppDbContext> o, TimeProvider timeProvider) : base(o)
    {
        _timeProvider = timeProvider;
    }

    public DbSet<MemberEntity> Members => Set<MemberEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<ContributionEntity> Contributions => Set<ContributionEntity>();
    public DbSet<ContributionPlanEntity> ContributionPlans => Set<ContributionPlanEntity>();
    public DbSet<MailSettingsEntity> MailSettings => Set<MailSettingsEntity>();
    public DbSet<LinkSettingsEntity> LinkSettings => Set<LinkSettingsEntity>();
    public DbSet<AuditLogEntity> AuditLogs => Set<AuditLogEntity>();
    public DbSet<CreditorConfigEntity> CreditorConfigs => Set<CreditorConfigEntity>();
    public DbSet<WebPageConfigEntity> WebPageConfigs => Set<WebPageConfigEntity>();
    public DbSet<SepaExportEntity> SepaExports => Set<SepaExportEntity>();
    public DbSet<MemberCategoryEntity> MemberCategories => Set<MemberCategoryEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserEntity>()
            .Property(u => u.Role)
            .HasConversion<string>();

        modelBuilder.Entity<UserEntity>()
            .HasQueryFilter(x => x.DeletedAt == null);


        modelBuilder.Entity<ContributionPlanEntity>()
            .Property(x => x.Interval)
            .HasConversion<string>();

        modelBuilder.Entity<ContributionPlanEntity>()
            .HasQueryFilter(x => x.DeletedAt == null);


        modelBuilder.Entity<MemberEntity>()
            .Property(x => x.TaskWithinTheClub)
            .HasConversion<string>();

        modelBuilder.Entity<MemberEntity>()
            .Property(x => x.Gender)
            .HasConversion<string>();

        modelBuilder.Entity<MemberEntity>()
            .Property(x => x.BulkMail)
            .HasConversion<string>();

        modelBuilder.Entity<MemberEntity>()
            .Property(x => x.AcademicDegree)
            .HasConversion<string>();


        modelBuilder.Entity<MemberCategoryEntity>()
            .HasQueryFilter(x => x.DeletedAt == null);

        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            var converter = new ValueConverter<DateTimeOffset?, long?>(
                v => v.HasValue ? v.Value.ToUnixTimeMilliseconds() : null,
                v => v.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(v.Value) : null
            );

            modelBuilder.Entity<ContributionEntity>()
                .Property(x => x.Paid)
                .HasConversion(converter);

            modelBuilder.Entity<ContributionEntity>()
                .Property(x => x.DeletedAt)
                .HasConversion(converter);
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker
            .Entries()
            .Where(e => e.Entity is BaseEntity && (e.State == EntityState.Added || e.State == EntityState.Deleted));

        foreach (var entityEntry in entries)
        {
            var entity = (BaseEntity)entityEntry.Entity;

            if (entityEntry.State == EntityState.Added)
            {
                entity.CreatedAt = _timeProvider.GetUtcNow();
            }
            else if (entityEntry.State == EntityState.Deleted)
            {
                entityEntry.State = EntityState.Modified;
                entity.DeletedAt = _timeProvider.GetUtcNow();
            }
        }

        return await base.SaveChangesAsync();
    }

    public async Task<int> ForceSaveChangesAsync()
    {
        var result = await base.SaveChangesAsync();
        ChangeTracker.Clear();
        return result;
    }
}