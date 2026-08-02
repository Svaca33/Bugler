using Bugler.Registry.Contracts;
using Microsoft.AspNetCore.Http;
using Npgsql;

namespace Bugler.Ingestion.ReportStorageFootprint;

/// <summary>
/// One signal kind's cost for one Service: the Footprint it holds, the days its Purge works
/// from, and what the measured window saw arrive. FootprintBytes and the window's bytes are
/// estimates and the UI labels them so; RetentionDays is exact — it is the same Effective
/// Retention the purge itself reads.
/// </summary>
public sealed record SignalStorageDto(
    long FootprintBytes,
    int RetentionDays,
    long WindowBytes);

/// <summary>One registered Service's storage picture, logs and traces on their own clocks.</summary>
public sealed record ServiceStorageDto(
    Guid ServiceId,
    SignalStorageDto Logs,
    SignalStorageDto Traces);

/// <summary>
/// The whole report: every registered Service — silent ones included, at zero — over one
/// measured window, plus what each telemetry table really occupies on disk (data, indexes and
/// TOAST together; the per-Service split of that number is the estimate, these two are not).
/// The Ingest Rate is the caller's division: WindowBytes over (WindowEnd − WindowStart).
/// </summary>
public sealed record StorageReportResponse(
    DateTime WindowStart,
    DateTime WindowEnd,
    long LogTableBytes,
    long TraceTableBytes,
    IReadOnlyList<ServiceStorageDto> Services);

internal static class ReportStorageEndpoint
{
    /// <summary>The window the Ingest Rate is measured over: the day behind the server's now.</summary>
    private static readonly TimeSpan Window = TimeSpan.FromHours(24);

    /// <summary>
    /// As the Volume's (Sql.VolumeTimeoutSeconds): the report walks one index per table end to
    /// end and the heap behind one day of Signals, which a loaded instance will feel.
    /// </summary>
    private const int TimeoutSeconds = 30;

    private const string LogColumnBytes =
        "pg_column_size(t.id) + pg_column_size(t.service_id) + pg_column_size(t.timestamp)"
        + " + coalesce(pg_column_size(t.observed_timestamp), 0)"
        + " + pg_column_size(t.severity_number)"
        + " + coalesce(pg_column_size(t.severity_text), 0)"
        + " + coalesce(pg_column_size(t.body), 0)"
        + " + coalesce(pg_column_size(t.trace_id), 0)"
        + " + coalesce(pg_column_size(t.span_id), 0)"
        + " + coalesce(pg_column_size(t.scope_name), 0)"
        + " + pg_column_size(t.resource_attributes) + pg_column_size(t.attributes)";

    private const string SpanColumnBytes =
        "pg_column_size(t.id) + pg_column_size(t.service_id) + pg_column_size(t.trace_id)"
        + " + pg_column_size(t.span_id)"
        + " + coalesce(pg_column_size(t.parent_span_id), 0)"
        + " + pg_column_size(t.name) + pg_column_size(t.kind)"
        + " + pg_column_size(t.start_time) + pg_column_size(t.end_time)"
        + " + pg_column_size(t.status_code)"
        + " + coalesce(pg_column_size(t.status_message), 0)"
        + " + coalesce(pg_column_size(t.scope_name), 0)"
        + " + pg_column_size(t.resource_attributes) + pg_column_size(t.attributes)"
        + " + pg_column_size(t.events) + pg_column_size(t.links)";

    public static async Task<IResult> Handle(
        IRetentionReader retentionReader,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        var retentions = await retentionReader.GetEffectiveRetentionsAsync(cancellationToken);
        var serviceIds = retentions.Select(r => r.ServiceId.Value).ToArray();

        try
        {
            // The server's clock, like every window Bugler measures (ADR 0002 in Exploration).
            var now = await ReadServerTimeAsync(dataSource, cancellationToken);
            var windowStart = now - Window;

            var (logTableBytes, traceTableBytes) =
                await ReadTableBytesAsync(dataSource, cancellationToken);

            var logRows = await ReadRowsByServiceAsync(
                "telemetry.log_records", dataSource, cancellationToken);
            var traceRows = await ReadRowsByServiceAsync(
                "telemetry.spans", dataSource, cancellationToken);

            var logWindows = await ReadWindowsAsync(
                "telemetry.log_records", "timestamp", LogColumnBytes,
                serviceIds, windowStart, now, dataSource, cancellationToken);
            var traceWindows = await ReadWindowsAsync(
                "telemetry.spans", "start_time", SpanColumnBytes,
                serviceIds, windowStart, now, dataSource, cancellationToken);

            var logFootprints = FootprintEstimator.Estimate(logTableBytes, logRows, logWindows);
            var traceFootprints = FootprintEstimator.Estimate(traceTableBytes, traceRows, traceWindows);

            var services = retentions
                .OrderBy(r => r.ServiceId.Value)
                .Select(r => new ServiceStorageDto(
                    r.ServiceId.Value,
                    Summarize(r.ServiceId.Value, r.LogDays, logFootprints, logWindows),
                    Summarize(r.ServiceId.Value, r.TraceDays, traceFootprints, traceWindows)))
                .ToList();

            return TypedResults.Ok(new StorageReportResponse(
                windowStart,
                now,
                logTableBytes,
                traceTableBytes,
                services));
        }
        catch (NpgsqlException) when (!cancellationToken.IsCancellationRequested)
        {
            return TypedResults.Problem(
                title: "The storage report could not be assembled in time.",
                detail: "The instance is busy; try again in a moment.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static SignalStorageDto Summarize(
        Guid serviceId,
        int retentionDays,
        IReadOnlyDictionary<Guid, long> footprints,
        IReadOnlyDictionary<Guid, FootprintEstimator.WindowSample> windows)
    {
        return new SignalStorageDto(
            footprints.GetValueOrDefault(serviceId),
            retentionDays,
            windows.GetValueOrDefault(serviceId)?.Bytes ?? 0);
    }

    private static async Task<DateTime> ReadServerTimeAsync(
        NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand("SELECT now()");
        var now = (DateTime)(await command.ExecuteScalarAsync(cancellationToken))!;
        return DateTime.SpecifyKind(now, DateTimeKind.Utc);
    }

    private static async Task<(long Logs, long Traces)> ReadTableBytesAsync(
        NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT pg_total_relation_size('telemetry.log_records'),"
            + " pg_total_relation_size('telemetry.spans')");
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return (reader.GetInt64(0), reader.GetInt64(1));
    }

    /// <summary>
    /// Every Service's row count, orphans of deleted Services included — they still occupy the
    /// table until the purge reclaims them, so they must keep their share of its bytes rather
    /// than have it smeared over the registered.
    /// </summary>
    private static async Task<Dictionary<Guid, long>> ReadRowsByServiceAsync(
        string table, NpgsqlDataSource dataSource, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            $"SELECT service_id, count(*) FROM {table} GROUP BY service_id");
        command.CommandTimeout = TimeoutSeconds;

        var rows = new Dictionary<Guid, long>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows[reader.GetGuid(0)] = reader.GetInt64(1);
        }

        return rows;
    }

    /// <summary>
    /// The measured window, one Service at a time: `unnest` with a LATERAL range scan keeps the
    /// planner on the (service_id, time) index for every Service instead of gambling on a whole-
    /// table scan — the same trap the log list measured and indexed its way out of (ADR 0012).
    /// </summary>
    private static async Task<Dictionary<Guid, FootprintEstimator.WindowSample>> ReadWindowsAsync(
        string table,
        string timeColumn,
        string columnBytes,
        Guid[] serviceIds,
        DateTime windowStart,
        DateTime windowEnd,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        var windows = new Dictionary<Guid, FootprintEstimator.WindowSample>();
        if (serviceIds.Length == 0)
        {
            return windows;
        }

        await using var command = dataSource.CreateCommand();
        command.CommandTimeout = TimeoutSeconds;
        command.CommandText = $"""
            SELECT s.id, count(*), sum({columnBytes})::bigint
            FROM unnest(@services) AS s(id)
            CROSS JOIN LATERAL (
                SELECT * FROM {table}
                WHERE service_id = s.id AND {timeColumn} >= @from AND {timeColumn} < @to
            ) AS t
            GROUP BY s.id
            """;
        command.Parameters.AddWithValue("services", serviceIds);
        command.Parameters.AddWithValue("from", windowStart);
        // The upper bound guards the division as much as the read: a sender's clock running
        // ahead may not stamp Signals into a window the rate is divided by.
        command.Parameters.AddWithValue("to", windowEnd);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var rows = reader.GetInt64(1);
            windows[reader.GetGuid(0)] = new FootprintEstimator.WindowSample(
                rows, reader.GetInt64(2) + rows * FootprintEstimator.RowOverheadBytes);
        }

        return windows;
    }
}
