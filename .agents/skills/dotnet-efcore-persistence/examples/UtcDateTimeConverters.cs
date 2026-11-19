using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MyApp.Infrastructure.Persistence.Converters;

/// <summary>
/// Ensures DateTime values stored in SQLite are always stored in ISO 8601 UTC format
/// and materialized with DateTimeKind.Utc.
/// </summary>
public sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter()
        : base(
            v => v.ToUniversalTime(),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
    {
    }
}

/// <summary>
/// Ensures DateTimeOffset values in SQLite preserve their exact UTC representation.
/// </summary>
public sealed class UtcDateTimeOffsetConverter : ValueConverter<DateTimeOffset, DateTimeOffset>
{
    public UtcDateTimeOffsetConverter()
        : base(
            v => v.ToUniversalTime(),
            v => v.ToUniversalTime())
    {
    }
}
