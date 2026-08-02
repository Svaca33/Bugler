using System.Xml;
using Bugler.Exploration.Querying;
using Bugler.Exploration.Scoping;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Bugler.Exploration.SummarizeLogVolume;

/// <summary>
/// The Volume of a query: its Resolved Window, the width every Bucket shares as an ISO-8601
/// duration, and one entry per Bucket including the empty ones.
/// <para>
/// <paramref name="Now"/> is the server instant the window was resolved against. Without it a
/// viewer cannot tell the Bucket that is still filling from one the window cut short: the window's
/// top is stretched to cover the newest record, so the Bucket holding it ends exactly at
/// <paramref name="To"/> and reads as complete while most of it has yet to elapse. Viewers must not
/// substitute their own clock for this — a Resolved Window is the server's reading, not theirs.
/// </para>
/// </summary>
public sealed record LogVolumeResponse(
    DateTime From,
    DateTime To,
    DateTime Now,
    string Bucket,
    IReadOnlyList<VolumeBucketDto> Buckets);

internal static class SummarizeLogVolumeEndpoint
{
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
            var now = await VolumeAggregate.ReadServerTimeAsync(dataSource, cancellationToken);

            // Pinning first means the rows counted and the window reported are the same window,
            // rather than two readings of a clock that moved in between.
            var pinned = criteria with { Time = criteria.Time.Pin(now) };

            var lower = pinned.Time.LowerBound(now)
                        ?? await VolumeAggregate.ReadOldestAsync(
                            pinned, serviceIds, visible, dataSource, cancellationToken)
                        ?? now - VolumeAggregate.EmptyWindow;

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
                now.UtcDateTime,
                XmlConvert.ToString(width),
                VolumeAggregate.Densify(counted, lower, end, width)));
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

    private static async Task<Dictionary<DateTimeOffset, VolumeBucketDto>> ReadBucketsAsync(
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
}
