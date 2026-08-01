using Npgsql;

namespace Bugler.Exploration.Querying;

/// <summary>Counts in one Bucket, split by Severity Band. Error carries FATAL, Debug carries TRACE and UNSET.</summary>
public sealed record VolumeBucketDto(DateTime Start, int Error, int Warn, int Info, int Debug);

/// <summary>
/// The window mechanics every Volume flavour shares — one clock, one lower-bound rule, one
/// densifier — so the chart and the board can never disagree about what a window is.
/// </summary>
internal static class VolumeAggregate
{
    /// <summary>What a window comes out as when nothing bounds it and nothing has been ingested to bound it.</summary>
    public static readonly TimeSpan EmptyWindow = TimeSpan.FromHours(1);

    /// <summary>Far past any real window at the top rung; guards only against a pathological payload.</summary>
    public const int MaxBuckets = 2000;

    public static async Task<DateTimeOffset> ReadServerTimeAsync(
        NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("SELECT now()");
        // Npgsql reads timestamptz as a UTC DateTime, never as a DateTimeOffset.
        return new DateTimeOffset((DateTime)(await command.ExecuteScalarAsync(cancellationToken))!);
    }

    /// <summary>
    /// Only reached when the Time Filter leaves the bottom open — a bare `to`, or no Time Filter at
    /// all. The common Relative Range never pays for this.
    /// </summary>
    public static async Task<DateTimeOffset?> ReadOldestAsync(
        LogCriteria criteria,
        Guid[]? serviceIds,
        bool visible,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        if (!visible)
        {
            return null;
        }

        await using var command = dataSource.CreateCommand();
        command.CommandTimeout = Sql.VolumeTimeoutSeconds;
        var where = criteria.Where(command, serviceIds);
        command.CommandText = $"SELECT min(timestamp) FROM telemetry.log_records{where}";

        var oldest = await command.ExecuteScalarAsync(cancellationToken);
        return oldest is DateTime instant ? new DateTimeOffset(Sql.EnsureUtc(instant)) : null;
    }

    /// <summary>
    /// Every Bucket from the one holding <paramref name="from"/> to the end of the window, empty ones
    /// included. Silence is the finding in an observability tool, and a client that had to synthesize
    /// the gaps itself would eventually forget to and quietly draw a shorter stretch of time.
    /// </summary>
    public static List<VolumeBucketDto> Densify(
        Dictionary<DateTimeOffset, VolumeBucketDto> counted,
        DateTimeOffset from,
        DateTimeOffset to,
        TimeSpan width)
    {
        var buckets = new List<VolumeBucketDto>();
        for (var start = BucketWidth.Floor(from, width); start < to && buckets.Count < MaxBuckets; start += width)
        {
            buckets.Add(counted.TryGetValue(start, out var bucket)
                ? bucket
                : new VolumeBucketDto(start.UtcDateTime, 0, 0, 0, 0));
        }

        return buckets;
    }
}
