using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using UniVerein.DAL.Entities;
using UniVerein.DAL.Entities.Enums;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace UniVerein.DAL.Data;

public class AppDbContext : DbContext
{
    private readonly TimeProvider _timeProvider;

    public AppDbContext(DbContextOptions<AppDbContext> o, TimeProvider timeProvider) : base(o)
    {
        _timeProvider = timeProvider;
    }

    // Skips CreatedAt/DeletedAt auto-stamping below, so UniVerein.DbMigrator can preserve original timestamps.
    public bool SuppressAutoTimestamps { get; set; }

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
    public DbSet<FirmwareVersionEntity> FirmwareVersions => Set<FirmwareVersionEntity>();
    public DbSet<FirmwareVersionNotificationEntity> FirmwareVersionNotifications => Set<FirmwareVersionNotificationEntity>();
    public DbSet<ReceiptEntity> Receipts => Set<ReceiptEntity>();
    public DbSet<ReceiptCategoryEntity> ReceiptCategories => Set<ReceiptCategoryEntity>();
    public DbSet<ReceiptFileEntity> ReceiptFiles => Set<ReceiptFileEntity>();
    public DbSet<UserSettingEntity> UserSettings => Set<UserSettingEntity>();
    public DbSet<PendingSelfEnrollmentEntity> PendingSelfEnrollments => Set<PendingSelfEnrollmentEntity>();
    public DbSet<CalendarEventEntity> CalendarEvents => Set<CalendarEventEntity>();
    public DbSet<CalendarCategoryEntity> CalendarCategories => Set<CalendarCategoryEntity>();
    public DbSet<CalendarEventParticipantEntity> CalendarEventParticipants => Set<CalendarEventParticipantEntity>();
    public DbSet<CalendarEventSubscriptionEntity> CalendarEventSubscriptions => Set<CalendarEventSubscriptionEntity>();
    public DbSet<NotificationEntity> Notifications => Set<NotificationEntity>();
    public DbSet<CalendarReminderLogEntity> CalendarReminderLogs => Set<CalendarReminderLogEntity>();
    public DbSet<CalendarFeedTokenEntity> CalendarFeedTokens => Set<CalendarFeedTokenEntity>();
    public DbSet<CalendarSettingsEntity> CalendarSettings => Set<CalendarSettingsEntity>();


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
            .Property(x => x.MembershipStatus)
            .HasConversion<string>()
            .HasDefaultValue(MembershipStatus.MEMBER);

        modelBuilder.Entity<MemberEntity>()
            .Property(x => x.BulkMail)
            .HasConversion<string>();

        modelBuilder.Entity<MemberEntity>()
            .Property(x => x.AcademicDegree)
            .HasConversion<string>();


        modelBuilder.Entity<MemberCategoryEntity>()
            .HasQueryFilter(x => x.DeletedAt == null);


        modelBuilder.Entity<FirmwareVersionEntity>()
            .HasIndex(f => f.Version)
            .IsUnique();


        modelBuilder.Entity<FirmwareVersionNotificationEntity>()
            .HasIndex(n => new { n.FirmwareVersionId, n.UserId })
            .IsUnique();

        modelBuilder.Entity<FirmwareVersionNotificationEntity>()
            .HasOne(n => n.FirmwareVersion)
            .WithMany(f => f.Notifications)
            .HasForeignKey(n => n.FirmwareVersionId);


        modelBuilder.Entity<ReceiptEntity>()
            .Property(x => x.PaymentMethod)
            .HasConversion<string>();

        modelBuilder.Entity<ReceiptEntity>()
            .HasMany(r => r.Files)
            .WithOne(i => i.Receipt)
            .HasForeignKey(i => i.ReceiptId);

        modelBuilder.Entity<ReceiptEntity>()
            .HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .IsRequired(false);

        modelBuilder.Entity<ReceiptCategoryEntity>()
            .HasQueryFilter(x => x.DeletedAt == null);


        modelBuilder.Entity<UserSettingEntity>()
            .Property(x => x.Type)
            .HasConversion<string>();

        modelBuilder.Entity<UserSettingEntity>()
            .HasIndex(x => new { x.UserId, x.Type })
            .IsUnique();

        modelBuilder.Entity<UserSettingEntity>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId);


        modelBuilder.Entity<PendingSelfEnrollmentEntity>()
            .Property(x => x.Status)
            .HasConversion<string>();

        modelBuilder.Entity<PendingSelfEnrollmentEntity>()
            .Property(x => x.Gender)
            .HasConversion<string>();

        modelBuilder.Entity<PendingSelfEnrollmentEntity>()
            .Property(x => x.BulkMail)
            .HasConversion<string>();

        modelBuilder.Entity<PendingSelfEnrollmentEntity>()
            .Property(x => x.AcademicDegree)
            .HasConversion<string>();

        modelBuilder.Entity<PendingSelfEnrollmentEntity>()
            .HasOne(x => x.MemberCategory)
            .WithMany()
            .HasForeignKey(x => x.MemberCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<PendingSelfEnrollmentEntity>()
            .HasOne(x => x.ContributionPlan)
            .WithMany()
            .HasForeignKey(x => x.ContributionPlanId)
            .OnDelete(DeleteBehavior.SetNull);


        modelBuilder.Entity<CalendarEventEntity>()
            .Property(x => x.Visibility)
            .HasConversion<string>();

        modelBuilder.Entity<CalendarEventEntity>()
            .Property(x => x.Version)
            .IsConcurrencyToken();

        modelBuilder.Entity<CalendarEventEntity>()
            .HasQueryFilter(x => x.DeletedAt == null);

        modelBuilder.Entity<CalendarEventEntity>()
            .HasIndex(x => new { x.StartAt, x.SeriesEndAt });

        modelBuilder.Entity<CalendarEventEntity>()
            .HasOne(x => x.Owner)
            .WithMany()
            .HasForeignKey(x => x.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CalendarEventEntity>()
            .HasOne(x => x.Category)
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<CalendarEventEntity>()
            .HasMany(x => x.Participants)
            .WithOne(x => x.Event)
            .HasForeignKey(x => x.EventId);

        modelBuilder.Entity<CalendarCategoryEntity>()
            .HasQueryFilter(x => x.DeletedAt == null);

        modelBuilder.Entity<CalendarEventParticipantEntity>()
            .Property(x => x.Role)
            .HasConversion<string>();

        modelBuilder.Entity<CalendarEventParticipantEntity>()
            .HasQueryFilter(x => x.DeletedAt == null);

        modelBuilder.Entity<CalendarEventParticipantEntity>()
            .HasIndex(x => new { x.EventId, x.UserId });

        modelBuilder.Entity<CalendarEventParticipantEntity>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);


        modelBuilder.Entity<CalendarEventParticipantEntity>()
            .Property(x => x.RsvpStatus)
            .HasConversion<string>();

        modelBuilder.Entity<CalendarEventSubscriptionEntity>()
            .HasQueryFilter(x => x.DeletedAt == null);

        modelBuilder.Entity<CalendarEventSubscriptionEntity>()
            .HasIndex(x => new { x.EventId, x.UserId });

        modelBuilder.Entity<CalendarEventSubscriptionEntity>()
            .HasOne(x => x.Event)
            .WithMany(x => x.Subscriptions)
            .HasForeignKey(x => x.EventId);

        modelBuilder.Entity<CalendarEventSubscriptionEntity>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);


        modelBuilder.Entity<NotificationEntity>()
            .Property(x => x.Type)
            .HasConversion<string>();

        modelBuilder.Entity<NotificationEntity>()
            .HasQueryFilter(x => x.DeletedAt == null);

        modelBuilder.Entity<NotificationEntity>()
            .HasIndex(x => new { x.UserId, x.ReadAt });

        modelBuilder.Entity<NotificationEntity>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);


        modelBuilder.Entity<CalendarReminderLogEntity>()
            .HasIndex(x => new { x.EventId, x.OccurrenceStart })
            .IsUnique();


        modelBuilder.Entity<CalendarFeedTokenEntity>()
            .HasIndex(x => x.TokenHash)
            .IsUnique();

        modelBuilder.Entity<CalendarFeedTokenEntity>()
            .HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);


        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            ValueConverter<DateTimeOffset?, long?> converter = new ValueConverter<DateTimeOffset?, long?>(
                v => v.HasValue ? v.Value.ToUnixTimeMilliseconds() : null,
                v => v.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(v.Value) : null
            );

            modelBuilder.Entity<ContributionEntity>()
                .Property(x => x.Paid)
                .HasConversion(converter);

            modelBuilder.Entity<ContributionEntity>()
                .Property(x => x.DeletedAt)
                .HasConversion(converter);

            modelBuilder.Entity<ReceiptEntity>()
                .Property(x => x.Paid)
                .HasConversion(converter);
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (SuppressAutoTimestamps)
            return await base.SaveChangesAsync(cancellationToken);

        IEnumerable<EntityEntry> entries = ChangeTracker
            .Entries()
            .Where(e => e.Entity is BaseEntity && (e.State == EntityState.Added || e.State == EntityState.Deleted));

        foreach (EntityEntry? entityEntry in entries)
        {
            BaseEntity entity = (BaseEntity)entityEntry.Entity;

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
        int result = await base.SaveChangesAsync();
        ChangeTracker.Clear();
        return result;
    }
}