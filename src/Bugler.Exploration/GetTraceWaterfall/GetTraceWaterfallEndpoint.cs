using System.Text.Json;
using Bugler.Exploration.Querying;
using Bugler.Exploration.Scoping;
using Microsoft.AspNetCore.Http;
using Npgsql;

namespace Bugler.Exploration.GetTraceWaterfall;

public sealed record TraceSpanDto(
    long Id,
    Guid ServiceId,
    string SpanId,
    string? ParentSpanId,
    string Name,
    short Kind,
    DateTime StartTime,
    DateTime EndTime,
    short StatusCode,
    string? StatusMessage,
    string? ScopeName,
    JsonElement ResourceAttributes,
    JsonElement Attributes,
    JsonElement Events,
    JsonElement Links);

public sealed record TraceDetailResponse(string TraceId, IReadOnlyList<TraceSpanDto> Spans);

internal static class GetTraceWaterfallEndpoint
{
    public static async Task<IResult> Handle(
        string traceId,
        ScopeResolver scope,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        var normalizedTraceId = traceId.ToLowerInvariant();

        // No Source Filter here on purpose: a Trace may cross Services, and the waterfall
        // must show every span the caller is allowed to see, not just one Service's.
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

        command.Parameters.AddWithValue("traceId", normalizedTraceId);
        command.CommandText = $"""
            SELECT id, service_id, span_id, parent_span_id, name, kind, start_time, end_time,
                   status_code, status_message, scope_name,
                   resource_attributes::text, attributes::text, events::text, links::text
            FROM telemetry.spans
            WHERE trace_id = @traceId{scopeFilter}
            ORDER BY start_time, id
            """;

        var spans = new List<TraceSpanDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            spans.Add(new TraceSpanDto(
                reader.GetInt64(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetString(4),
                reader.GetInt16(5),
                reader.GetDateTime(6),
                reader.GetDateTime(7),
                reader.GetInt16(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                Sql.ParseJson(reader.GetString(11)),
                Sql.ParseJson(reader.GetString(12)),
                Sql.ParseJson(reader.GetString(13)),
                Sql.ParseJson(reader.GetString(14))));
        }

        return spans.Count > 0
            ? TypedResults.Ok(new TraceDetailResponse(normalizedTraceId, spans))
            : TypedResults.NotFound();
    }
}
