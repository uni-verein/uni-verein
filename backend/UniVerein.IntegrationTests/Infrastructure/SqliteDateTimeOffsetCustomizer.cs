using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace UniVerein.IntegrationTests.Infrastructure;

public class SqliteDateTimeOffsetCustomizer : ModelCustomizer
{
    public SqliteDateTimeOffsetCustomizer(ModelCustomizerDependencies deps) : base(deps)
    {
    }

    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    {
        base.Customize(modelBuilder, context);

        ValueConverter<DateTimeOffset, long> converter = new ValueConverter<DateTimeOffset, long>(
            v => v.ToUnixTimeMilliseconds(),
            v => DateTimeOffset.FromUnixTimeMilliseconds(v)
        );

        ValueConverter<DateTimeOffset?, long?> nullableConverter = new ValueConverter<DateTimeOffset?, long?>(
            v => v.HasValue ? v.Value.ToUnixTimeMilliseconds() : null,
            v => v.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(v.Value) : null
        );

        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
            foreach (IMutableProperty? property in entityType.GetProperties()
                         .Where(p => p.ClrType == typeof(DateTimeOffset)))
            {
                property.SetValueConverter(converter);

                if (property.ClrType == typeof(DateTimeOffset?))
                    property.SetValueConverter(nullableConverter);
            }
    }
}