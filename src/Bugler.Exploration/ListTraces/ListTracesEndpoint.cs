using Bugler.Exploration.Querying;
using Bugler.Exploration.Scoping;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Bugler.Exploration.ListTraces;

public sealed record TraceSummaryDto(
    string TraceId,
    DateTime StartTime,
    double DurationMs,
    int SpanCount,
    bool HasError,
    string? RootName,
    Guid? RootServiceId);

public sealed record ListTracesResponse(IReadOnlyList<TraceSummaryDto> Items);

internal static class ListTracesEndpoint
{
    public static async Task<IResult> Handle(
        Guid? applicationId,
        [FromQuery(Name = "namespace")] string? serviceNamespace,
        string? environment,
        string? service,
        DateTime? from,
        DateTime? to,
        bool? errorsOnly,
        string[]? attr,
        string[]? res,
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

        var serviceIds = await scope.ResolveServiceIdsAsync(
            SourceFilter.FromQuery(applicationId, serviceNamespace, environment, service),
            cancellationToken);

        if (serviceIds is { Length: 0 })
        {
            return TypedResults.Ok(new ListTracesResponse([]));
        }

        await using var command = dataSource.CreateCommand();
        var conditions = new List<string>();

        if (serviceIds is not null)
        {
            conditions.Add("service_id = ANY(@services)");
            command.Parameters.AddWithValue("services", serviceIds);
        }

        if (from is { } fromTime)
        {
            conditions.Add("start_time >= @from");
            command.Parameters.AddWithValue("from", Sql.EnsureUtc(fromTime));
        }

        if (to is { } toTime)
        {
            conditions.Add("start_time <= @to");
            command.Parameters.AddWithValue("to", Sql.EnsureUtc(toTime));
        }

        if (attributeFilters.Length > 0 || resourceFilters.Length > 0)
        {
            // Same-span semantics (ADR 0001): one span must satisfy every filter at once.
            var spanConditions = new List<string>();
            if (serviceIds is not null)
            {
                spanConditions.Add("m.service_id = ANY(@services)");
            }

            AttributeFilters.AddConditions(spanConditions, command, attributeFilters, "m.attributes", "attr");
            AttributeFilters.AddConditions(spanConditions, command, resourceFilters, "m.resource_attributes", "res");
            conditions.Add(
                $"trace_id IN (SELECT m.trace_id FROM telemetry.spans m WHERE {string.Join(" AND ", spanConditions)})");
        }

        var take = Math.Clamp(limit ?? 50, 1, 500);
        var where = conditions.Count > 0 ? $" WHERE {string.Join(" AND ", conditions)}" : "";
        var having = errorsOnly is true ? " HAVING bool_or(status_code = 2)" : "";
        command.CommandText = $"""
            SELECT trace_id,
                   min(start_time) AS start_time,
                   (EXTRACT(EPOCH FROM (max(end_time) - min(start_time))) * 1000)::double precision AS duration_ms,
                   count(*)::int AS span_count,
                   bool_or(status_code = 2) AS has_error,
                   (array_agg(name ORDER BY (parent_span_id IS NULL) DESC, start_time))[1] AS root_name,
                   (array_agg(service_id ORDER BY (parent_span_id IS NULL) DESC, start_time))[1] AS root_service_id
            FROM telemetry.spans{where}
            GROUP BY trace_id{having}
            ORDER BY min(start_time) DESC
            LIMIT {take}
            """;

        var items = new List<TraceSummaryDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new TraceSummaryDto(
                reader.GetString(0),
                reader.GetDateTime(1),
                reader.GetDouble(2),
                reader.GetInt32(3),
                reader.GetBoolean(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetGuid(6)));
        }

        return TypedResults.Ok(new ListTracesResponse(items));
    }
}
