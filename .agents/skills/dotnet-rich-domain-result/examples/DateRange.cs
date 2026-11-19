namespace MyApp.Domain.Common;

/// <summary>
/// Immutable Value Object representing a chronological date-time range in UTC.
/// Encapsulates boundary validation and overlap logic.
/// </summary>
public sealed record DateRange
{
    public DateTimeOffset StartUtc { get; }
    public DateTimeOffset EndUtc { get; }

    public TimeSpan Duration => EndUtc - StartUtc;

    private DateRange(DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        StartUtc = startUtc;
        EndUtc = endUtc;
    }

    /// <summary>
    /// Factory method ensuring StartUtc is strictly prior to EndUtc.
    /// </summary>
    public static Result<DateRange> Create(DateTimeOffset startUtc, DateTimeOffset endUtc)
    {
        if (startUtc == default)
        {
            return Result<DateRange>.Failure(
                Error.Validation("DateRange.StartDefault", "Start date cannot be default."));
        }

        if (endUtc == default)
        {
            return Result<DateRange>.Failure(
                Error.Validation("DateRange.EndDefault", "End date cannot be default."));
        }

        if (startUtc >= endUtc)
        {
            return Result<DateRange>.Failure(
                Error.Validation("DateRange.InvalidInterval", "Start date must be strictly earlier than end date."));
        }

        return Result<DateRange>.Success(new DateRange(startUtc, endUtc));
    }

    /// <summary>
    /// Evaluates whether this date range overlaps with another date range.
    /// Uses half-open interval semantics [Start, End): two ranges overlap if
    /// this.Start < other.End && this.End > other.Start.
    /// </summary>
    public bool Overlaps(DateRange other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return StartUtc < other.EndUtc && EndUtc > other.StartUtc;
    }

    /// <summary>
    /// Verifies if a given timestamp falls within the range [StartUtc, EndUtc).
    /// </summary>
    public bool Contains(DateTimeOffset timestampUtc)
    {
        return timestampUtc >= StartUtc && timestampUtc < EndUtc;
    }
}
