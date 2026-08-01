using System.Xml;
using Bugler.Exploration.Querying;
using Bugler.Exploration.Scoping;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Bugler.Exploration.SummarizeLogVolumeByService;

/// <summary>
/// One Service's share of the window: totals per Severity Band, and Buckets densified over the
/// same axis every other Service got — that is what makes two entries comparable side by side.
/// </summary>
public sealed record ServiceVolumeDto(
    Guid ServiceId,
    int Error,
    int Warn,
    int Info,
    int Debug,
    IReadOnlyList<VolumeBucketDto> Buckets);

/// <summary>
/// The Volume broken down by Service. A Service that logged nothing in the window is absent —
/// the caller renders it from the catalog with zeros rather than this response densifying a list
/// whose membership belongs to Registry.
/// </summary>
public sealed record LogVolumeByServiceResponse(
    DateTime From,
    DateTime To,
    string Bucket,
    IReadOnlyList<ServiceVolumeDto> Services);

internal static class SummarizeLogVolumeByServiceEndpoint
{
    /// <summary>About what a card-sized sparkline can show; far below what the ladder would pick for a wide window.</summary>
    private const int DefaultBuckets = 24;

    private const int MinRequestedBuckets = 8;
    private const int MaxRequestedBuckets = 64;

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
        int? buckets,
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
            var now = await VolumeAggregate.ReadServerTimeAsync(dataSource, cancellationToken);

            // Pinning first means the rows counted and the window reported are the same window,
            // rather than two readings of a clock that moved in between.
            var pinned = criteria with { Time = criteria.Time.Pin(now) };

            var lower = pinned.Time.LowerBound(now)
                        ?? await VolumeAggregate.ReadOldestAsync(
                            pinned, serviceIds, visible, dataSource, cancellationToken)
                        ?? now - VolumeAggregate.EmptyWindow;

            var ceiling = pinned.Time.UpperBound ?? now;
            var window = ceiling > lower ? ceiling - lower : TimeSpan.Zero;

            // A sparkline a few dozen pixels wide cannot show the ~2000 Buckets the natural rung
            // would produce for a wide window, so the caller says how many it can draw and the
            // width is ceil(window / that), rounded up to the ladder.
            var target = Math.Clamp(buckets ?? DefaultBuckets, MinRequestedBuckets, MaxRequestedBuckets);
            var width = BucketWidth.AtLeast(TimeSpan.FromTicks((window.Ticks + target - 1) / target));

            var counted = visible
                ? await ReadServiceBucketsAsync(pinned, serviceIds, width, dataSource, cancellationToken)
                : [];

            // One shared top for every Service: telemetry stamped beyond the ceiling stretches the
            // axis for the whole board, or two sparklines would end at different instants.
            var last = counted.Count > 0 ? counted.Max(r => r.Start) + width : ceiling;
            var end = last > ceiling ? last : ceiling;

            var services = counted
                .GroupBy(r => r.ServiceId)
                .OrderBy(g => g.Key)
                .Select(g => Summarize(g.Key, [.. g], lower, end, width))
                .ToList();

            return TypedResults.Ok(new LogVolumeByServiceResponse(
                lower.UtcDateTime,
                end.UtcDateTime,
                XmlConvert.ToString(width),
                services));
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

    private sealed record CountedRow(Guid ServiceId, DateTimeOffset Start, VolumeBucketDto Bucket);

    private static ServiceVolumeDto Summarize(
        Guid serviceId,
        IReadOnlyList<CountedRow> rows,
        DateTimeOffset from,
        DateTimeOffset to,
        TimeSpan width)
    {
        var byStart = rows.ToDictionary(r => r.Start, r => r.Bucket);
        return new ServiceVolumeDto(
            serviceId,
            rows.Sum(r => r.Bucket.Error),
            rows.Sum(r => r.Bucket.Warn),
            rows.Sum(r => r.Bucket.Info),
            rows.Sum(r => r.Bucket.Debug),
            VolumeAggregate.Densify(byStart, from, to, width));
    }

    private static async Task<List<CountedRow>> ReadServiceBucketsAsync(
        LogCriteria criteria,
        Guid[]? serviceIds,
        TimeSpan width,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand();
        command.CommandTimeout = Sql.VolumeTimeoutSeconds;
        var where = criteria.Where(command, serviceIds);
        command.Parameters.AddWithValue("width", width);

        // date_bin from the epoch keeps edges on multiples of the width in UTC (ADR 0003).
        command.CommandText = $"""
            SELECT service_id,
                   date_bin(@width, timestamp, TIMESTAMPTZ '1970-01-01 00:00:00+00') AS bucket,
                   count(*) FILTER (WHERE severity_number >= 17)::int AS error,
                   count(*) FILTER (WHERE severity_number BETWEEN 13 AND 16)::int AS warn,
                   count(*) FILTER (WHERE severity_number BETWEEN 9 AND 12)::int AS info,
                   count(*) FILTER (WHERE severity_number < 9)::int AS debug
            FROM telemetry.log_records{where}
            GROUP BY service_id, bucket
            ORDER BY service_id, bucket
            """;

        var rows = new List<CountedRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var start = new DateTimeOffset(Sql.EnsureUtc(reader.GetDateTime(1)));
            rows.Add(new CountedRow(
                reader.GetGuid(0),
                start,
                new VolumeBucketDto(
                    start.UtcDateTime,
                    reader.GetInt32(2),
                    reader.GetInt32(3),
                    reader.GetInt32(4),
                    reader.GetInt32(5))));
        }

        return rows;
    }
}
