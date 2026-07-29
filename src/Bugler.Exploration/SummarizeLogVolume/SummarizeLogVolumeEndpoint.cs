using System.Xml;
using Bugler.Exploration.Querying;
using Bugler.Exploration.Scoping;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Bugler.Exploration.SummarizeLogVolume;

/// <summary>Counts in one Bucket, split by Severity Band. Error carries FATAL, Debug carries TRACE and UNSET.</summary>
public sealed record VolumeBucketDto(DateTime Start, int Error, int Warn, int Info, int Debug);

/// <summary>
/// The Volume of a query: its Resolved Window, the width every Bucket shares as an ISO-8601
/// duration, and one entry per Bucket including the empty ones.
/// </summary>
public sealed record LogVolumeResponse(
    DateTime From,
    DateTime To,
    string Bucket,
    IReadOnlyList<VolumeBucketDto> Buckets);

internal static class SummarizeLogVolumeEndpoint
{
    /// <summary>What a window comes out as when nothing bounds it and nothing has been ingested to bound it.</summary>
    private static readonly TimeSpan EmptyWindow = TimeSpan.FromHours(1);

    /// <summary>Far past any real window at the top rung; guards only against a pathological payload.</summary>
    private const int MaxBuckets = 2000;

    public static async Task<IResult> Handle(
        Guid? applicationId,
        [FromQuery(Name = "namespace")] string? serviceNamespace,
        string? environment,
        string? service,
        short? severityMin,
        RelativeRange? range,
        RangeBound? from,
        RangeBound? to,
        string? q,
        string? traceId,
        string[]? attr,
        string[]? res,
        ScopeResolver scope,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        if (!LogCriteria.TryCreate(
                applicationId, serviceNamespace, environment, service, severityMin,
                range, from, to, q, traceId, attr, res, out var criteria, out var error))
        {
            return TypedResults.BadRequest(error);
        }

        var serviceIds = await scope.ResolveServiceIdsAsync(criteria.Source, cancellationToken);
        var visible = serviceIds is not { Length: 0 };

        try
        {
            // The server's clock, not the app's and certainly not the viewer's: it is the one the
            // list's own `now() - interval` is resolved against (ADR 0002).
            var now = await ReadServerTimeAsync(dataSource, cancellationToken);

            // Pinning first means the rows counted and the window reported are the same window,
            // rather than two readings of a clock that moved in between.
            var pinned = criteria with { Time = criteria.Time.Pin(now) };

            var lower = pinned.Time.LowerBound(now)
                        ?? await ReadOldestAsync(pinned, serviceIds, visible, dataSource, cancellationToken)
                        ?? now - EmptyWindow;

            // An open top resolves to `now` for the purpose of choosing a width; telemetry stamped
            // beyond it still lands in Buckets of its own and stretches `To` below (ADR 0003).
            var ceiling = pinned.Time.UpperBound ?? now;
            var width = BucketWidth.For(ceiling > lower ? ceiling - lower : TimeSpan.Zero);

            var counted = visible
                ? await ReadBucketsAsync(pinned, serviceIds, width, dataSource, cancellationToken)
                : [];

            var last = counted.Count > 0 ? counted.Keys.Max() + width : ceiling;
            var end = last > ceiling ? last : ceiling;

            return TypedResults.Ok(new LogVolumeResponse(
                lower.UtcDateTime,
                end.UtcDateTime,
                XmlConvert.ToString(width),
                Densify(counted, lower, end, width)));
        }
        catch (NpgsqlException) when (!cancellationToken.IsCancellationRequested)
        {
            // The list answers from the same filters without aggregating, so it is still standing.
            return TypedResults.Problem(
                title: "Volume could not be summarized in time.",
                detail: "Narrow the Time Filter and try again.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static async Task<DateTimeOffset> ReadServerTimeAsync(
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
    private static async Task<DateTimeOffset?> ReadOldestAsync(
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
        command.CommandTimeout = Sql.AggregateTimeoutSeconds;
        var where = criteria.Where(command, serviceIds);
        command.CommandText = $"SELECT min(timestamp) FROM telemetry.log_records{where}";

        var oldest = await command.ExecuteScalarAsync(cancellationToken);
        return oldest is DateTime instant ? new DateTimeOffset(Sql.EnsureUtc(instant)) : null;
    }

    private static async Task<Dictionary<DateTimeOffset, VolumeBucketDto>> ReadBucketsAsync(
        LogCriteria criteria,
        Guid[]? serviceIds,
        TimeSpan width,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand();
        command.CommandTimeout = Sql.AggregateTimeoutSeconds;
        var where = criteria.Where(command, serviceIds);
        command.Parameters.AddWithValue("width", width);

        // date_bin from the epoch keeps edges on multiples of the width in UTC (ADR 0003).
        command.CommandText = $"""
            SELECT date_bin(@width, timestamp, TIMESTAMPTZ '1970-01-01 00:00:00+00') AS bucket,
                   count(*) FILTER (WHERE severity_number >= 17)::int AS error,
                   count(*) FILTER (WHERE severity_number BETWEEN 13 AND 16)::int AS warn,
                   count(*) FILTER (WHERE severity_number BETWEEN 9 AND 12)::int AS info,
                   count(*) FILTER (WHERE severity_number < 9)::int AS debug
            FROM telemetry.log_records{where}
            GROUP BY bucket
            ORDER BY bucket
            """;

        var buckets = new Dictionary<DateTimeOffset, VolumeBucketDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var start = new DateTimeOffset(Sql.EnsureUtc(reader.GetDateTime(0)));
            buckets[start] = new VolumeBucketDto(
                start.UtcDateTime,
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetInt32(3),
                reader.GetInt32(4));
        }

        return buckets;
    }

    /// <summary>
    /// Every Bucket from the one holding <paramref name="from"/> to the end of the window, empty ones
    /// included. Silence is the finding in an observability tool, and a client that had to synthesize
    /// the gaps itself would eventually forget to and quietly draw a shorter stretch of time.
    /// </summary>
    private static List<VolumeBucketDto> Densify(
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
