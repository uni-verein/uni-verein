using System.Reflection;
using Microsoft.EntityFrameworkCore;
using UniVerein.DAL.Data;
using UniVerein.DAL.Entities;

var sourceConnectionString = Environment.GetEnvironmentVariable("SOURCE_CONNECTION_STRING");
var targetConnectionString = Environment.GetEnvironmentVariable("TARGET_CONNECTION_STRING");

if (string.IsNullOrWhiteSpace(sourceConnectionString) || string.IsNullOrWhiteSpace(targetConnectionString))
{
    Console.Error.WriteLine("SOURCE_CONNECTION_STRING (MariaDB) and TARGET_CONNECTION_STRING (PostgreSQL) must both be set.");
    return 1;
}

const int batchSize = 500;

try
{
    var sourceOptions = new DbContextOptionsBuilder<AppDbContext>()
        .UseMySql(sourceConnectionString, ServerVersion.AutoDetect(sourceConnectionString))
        .Options;

    var targetOptions = new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(targetConnectionString)
        .Options;

    await using var source = new AppDbContext(sourceOptions, TimeProvider.System);
    await using var target = new AppDbContext(targetOptions, TimeProvider.System);

    Console.WriteLine("Applying PostgreSQL schema migrations to target database...");
    await target.Database.MigrateAsync();

    // Preserve the original CreatedAt/DeletedAt values from MariaDB instead of stamping "now".
    target.SuppressAutoTimestamps = true;

    // Parent tables before child tables, so foreign keys resolve on insert.
    await CopyTableAsync("MemberCategories", source.MemberCategories, target, batchSize);
    await CopyTableAsync("ContributionPlans", source.ContributionPlans, target, batchSize);
    await CopyTableAsync("CreditorConfigs", source.CreditorConfigs, target, batchSize);
    await CopyTableAsync("LinkSettings", source.LinkSettings, target, batchSize);
    await CopyTableAsync("MailSettings", source.MailSettings, target, batchSize);
    await CopyTableAsync("WebPageConfigs", source.WebPageConfigs, target, batchSize);
    await CopyTableAsync("Users", source.Users, target, batchSize);
    await CopyTableAsync("Members", source.Members, target, batchSize);
    await CopyTableAsync("SepaExports", source.SepaExports, target, batchSize);
    await CopyTableAsync("Contributions", source.Contributions, target, batchSize);
    await CopyTableAsync("AuditLogs", source.AuditLogs, target, batchSize);
    await CopyTableAsync("FirmwareVersions", source.FirmwareVersions, target, batchSize);
    await CopyTableAsync("FirmwareVersionNotifications", source.FirmwareVersionNotifications, target, batchSize);

    Console.WriteLine("Migration completed successfully: all tables copied and row counts verified.");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Migration failed: {ex}");
    return 1;
}

static async Task CopyTableAsync<TEntity>(
    string tableName,
    DbSet<TEntity> sourceSet,
    AppDbContext target,
    int batchSize) where TEntity : BaseEntity
{
    Console.WriteLine($"Copying {tableName}...");

    // IgnoreQueryFilters: soft-deleted rows (DeletedAt != null) must be migrated too, not just active ones.
    var rows = await sourceSet.IgnoreQueryFilters().AsNoTracking().ToListAsync();

    // MariaDB has no timezone concept, so Pomelo returns plain DateTime columns (e.g. Contribution.DueDate)
    // with Kind=Unspecified. Npgsql refuses to write those into "timestamp with time zone" columns; the
    // app stores these as UTC instants, so tag them as such before inserting.
    var dateTimeProperties = typeof(TEntity).GetProperties()
        .Where(p => p.PropertyType == typeof(DateTime) || p.PropertyType == typeof(DateTime?))
        .ToArray();

    foreach (var row in rows)
    {
        foreach (var property in dateTimeProperties)
        {
            if (property.GetValue(row) is DateTime { Kind: not DateTimeKind.Utc } value)
            {
                property.SetValue(row, DateTime.SpecifyKind(value, DateTimeKind.Utc));
            }
        }
    }

    for (var i = 0; i < rows.Count; i += batchSize)
    {
        target.Set<TEntity>().AddRange(rows.Skip(i).Take(batchSize));
        await target.SaveChangesAsync();
        target.ChangeTracker.Clear();
    }

    var targetCount = await target.Set<TEntity>().IgnoreQueryFilters().CountAsync();
    if (targetCount != rows.Count)
    {
        throw new InvalidOperationException(
            $"{tableName}: expected {rows.Count} rows from source, but target has {targetCount} after copy.");
    }

    Console.WriteLine($"  {tableName}: {rows.Count} rows copied and verified.");
}
