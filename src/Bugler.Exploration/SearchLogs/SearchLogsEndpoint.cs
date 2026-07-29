using System.Text.Json;
using Bugler.Exploration.Querying;
using Bugler.Exploration.Scoping;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Bugler.Exploration.SearchLogs;

public sealed record LogRecordDto(
    long Id,
    Guid ServiceId,
    DateTime Timestamp,
    DateTime? ObservedTimestamp,
    short SeverityNumber,
    string? SeverityText,
    string? Body,
    string? TraceId,
    string? SpanId,
    string? ScopeName,
    JsonElement ResourceAttributes,
    JsonElement Attributes);

public sealed record SearchLogsResponse(IReadOnlyList<LogRecordDto> Items);

/// <summary>How many Log Records the Filter matches in total, whatever the list has paged in so far.</summary>
public sealed record LogCountResponse(int Total);

internal static class SearchLogsEndpoint
{
    private const string Columns =
        "id, service_id, timestamp, observed_timestamp, severity_number, severity_text, " +
        "body, trace_id, span_id, scope_name, resource_attributes::text, attributes::text";

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
        DateTime? before,
        long? beforeId,
        int? limit,
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

        if (serviceIds is { Length: 0 })
        {
            return TypedResults.Ok(new SearchLogsResponse([]));
        }

        await using var command = dataSource.CreateCommand();
        var conditions = new List<string>();
        criteria.AddConditions(conditions, command, serviceIds);

        if (before is { } beforeTime && beforeId is { } beforeIdValue)
        {
            conditions.Add("(timestamp, id) < (@before, @beforeId)");
            command.Parameters.AddWithValue("before", Sql.EnsureUtc(beforeTime));
            command.Parameters.AddWithValue("beforeId", beforeIdValue);
        }

        var take = Math.Clamp(limit ?? 100, 1, 1000);
        var where = conditions.Count > 0 ? $" WHERE {string.Join(" AND ", conditions)}" : "";
        command.CommandText =
            $"SELECT {Columns} FROM telemetry.log_records{where} ORDER BY timestamp DESC, id DESC LIMIT {take}";

        var items = new List<LogRecordDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadRow(reader));
        }

        return TypedResults.Ok(new SearchLogsResponse(items));
    }

    /// <summary>
    /// The total behind the page. It belongs to the list rather than to the list's Volume even though
    /// the Volume's Buckets would sum to it: a total read off the chart would have nowhere to come
    /// from whenever the chart is not on screen. It depends on the Filter and not on how far the
    /// reader has paged, so it is asked once per Filter and not once per page.
    /// </summary>
    public static async Task<IResult> HandleCount(
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
        if (serviceIds is { Length: 0 })
        {
            return TypedResults.Ok(new LogCountResponse(0));
        }

        try
        {
            await using var command = dataSource.CreateCommand();
            command.CommandTimeout = Sql.AggregateTimeoutSeconds;
            var where = criteria.Where(command, serviceIds);
            command.CommandText = $"SELECT count(*)::int FROM telemetry.log_records{where}";

            return TypedResults.Ok(new LogCountResponse((int)(await command.ExecuteScalarAsync(cancellationToken))!));
        }
        catch (NpgsqlException) when (!cancellationToken.IsCancellationRequested)
        {
            // The page itself is unaffected, so the list falls back to naming what it has loaded.
            return TypedResults.Problem(
                title: "The total could not be counted in time.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    public static async Task<IResult> HandleDetail(
        long id,
        ScopeResolver scope,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        var serviceIds = await scope.ResolveServiceIdsAsync(SourceFilter.None, cancellationToken);
        if (serviceIds is { Length: 0 })
        {
            return TypedResults.NotFound();
        }

        await using var command = dataSource.CreateCommand();
        var scopeFilter = serviceIds is null ? "" : " AND service_id = ANY(@services)";
        if (serviceIds is not null)
        {
            command.Parameters.AddWithValue("services", serviceIds);
        }

        command.Parameters.AddWithValue("id", id);
        command.CommandText = $"SELECT {Columns} FROM telemetry.log_records WHERE id = @id{scopeFilter}";

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? TypedResults.Ok(ReadRow(reader))
            : TypedResults.NotFound();
    }

    private static LogRecordDto ReadRow(NpgsqlDataReader reader) => new(
        reader.GetInt64(0),
        reader.GetGuid(1),
        reader.GetDateTime(2),
        reader.IsDBNull(3) ? null : reader.GetDateTime(3),
        reader.GetInt16(4),
        reader.IsDBNull(5) ? null : reader.GetString(5),
        reader.IsDBNull(6) ? null : reader.GetString(6),
        reader.IsDBNull(7) ? null : reader.GetString(7),
        reader.IsDBNull(8) ? null : reader.GetString(8),
        reader.IsDBNull(9) ? null : reader.GetString(9),
        Sql.ParseJson(reader.GetString(10)),
        Sql.ParseJson(reader.GetString(11)));
}
