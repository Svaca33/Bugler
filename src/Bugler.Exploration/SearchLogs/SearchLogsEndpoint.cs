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
        if (!AttributeFilters.TryParse(attr, out var attributeFilters) ||
            !AttributeFilters.TryParse(res, out var resourceFilters))
        {
            return TypedResults.BadRequest("Invalid attribute filter.");
        }

        if (!TimeFilter.TryCreate(range, from, to, out var timeFilter, out var timeError))
        {
            return TypedResults.BadRequest(timeError);
        }

        var serviceIds = await scope.ResolveServiceIdsAsync(
            SourceFilter.FromQuery(applicationId, serviceNamespace, environment, service),
            cancellationToken);

        if (serviceIds is { Length: 0 })
        {
            return TypedResults.Ok(new SearchLogsResponse([]));
        }

        await using var command = dataSource.CreateCommand();
        var conditions = new List<string>();

        if (serviceIds is not null)
        {
            conditions.Add("service_id = ANY(@services)");
            command.Parameters.AddWithValue("services", serviceIds);
        }

        timeFilter.AddConditions(conditions, command, "timestamp");

        if (severityMin is > 0)
        {
            conditions.Add("severity_number >= @severityMin");
            command.Parameters.AddWithValue("severityMin", severityMin.Value);
        }

        AttributeFilters.AddConditions(conditions, command, attributeFilters, "attributes", "attr");
        AttributeFilters.AddConditions(conditions, command, resourceFilters, "resource_attributes", "res");

        if (!string.IsNullOrEmpty(q))
        {
            conditions.Add(@"body ILIKE @q ESCAPE '\'");
            command.Parameters.AddWithValue("q", $"%{Sql.EscapeLike(q)}%");
        }

        if (!string.IsNullOrEmpty(traceId))
        {
            conditions.Add("trace_id = @traceId");
            command.Parameters.AddWithValue("traceId", traceId.ToLowerInvariant());
        }

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
