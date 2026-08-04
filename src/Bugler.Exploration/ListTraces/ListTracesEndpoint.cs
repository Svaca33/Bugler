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

/// <summary>
/// The page, and whether it is the whole answer. <paramref name="Truncated"/> is only ever true for
/// an error search that ran out of candidates before the page filled (ADR 0026): older matching
/// traces may exist that this answer never looked at.
/// </summary>
public sealed record ListTracesResponse(IReadOnlyList<TraceSummaryDto> Items, bool Truncated);

internal static class ListTracesEndpoint
{
    /// <summary>
    /// How wide an `errorsOnly` list draws to fill one page, as a multiple of the page and as a hard
    /// ceiling. Whether a trace failed is known only after its spans are aggregated, so the draw has
    /// to outnumber the page — but a window holding no errors at all must not be allowed to grow the
    /// draw back into the whole-window scan this endpoint exists to avoid.
    /// </summary>
    private const int ErrorSearchFactor = 100;
    private const int ErrorSearchCeiling = 20_000;

    public static async Task<IResult> Handle(
        Guid? applicationId,
        [FromQuery(Name = "namespace")] string? serviceNamespace,
        string? environment,
        string? service,
        RelativeRange? range,
        RangeBound? from,
        RangeBound? to,
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

        if (!TimeFilter.TryCreate(range, from, to, out var timeFilter, out var timeError))
        {
            return TypedResults.BadRequest(timeError);
        }

        var serviceIds = await scope.ResolveServiceIdsAsync(
            SourceFilter.FromQuery(applicationId, serviceNamespace, environment, service),
            cancellationToken);

        if (serviceIds is { Length: 0 })
        {
            return TypedResults.Ok(new ListTracesResponse([], false));
        }

        var take = Math.Clamp(limit ?? 50, 1, 500);
        var wanted = errorsOnly is true ? Math.Min(take * ErrorSearchFactor, ErrorSearchCeiling) : take;

        // Two steps, not one (ADR 0026). Grouping the window by `trace_id` and ordering by an
        // aggregate is work `LIMIT` cannot stop: every span in the window is read to learn which
        // hundred traces are the newest. Picking the page from Root Spans first is an ordered index
        // scan that `LIMIT` does stop, and only the traces on it are then aggregated.
        var candidates = await SelectCandidatesAsync(
            dataSource, serviceIds, timeFilter, attributeFilters, resourceFilters, wanted, cancellationToken);

        if (candidates.Length == 0)
        {
            return TypedResults.Ok(new ListTracesResponse([], false));
        }

        var items = await SummarizeAsync(
            dataSource, serviceIds, timeFilter, candidates, errorsOnly is true, take, cancellationToken);

        // The draw was spent before the page filled, so there is window left unexamined. An
        // unfiltered page draws exactly as many candidates as it returns and never reaches this.
        var truncated = items.Count < take && candidates.Length == wanted;
        return TypedResults.Ok(new ListTracesResponse(items, truncated));
    }

    /// <summary>
    /// The newest traces in the window, named by their Root Span. A trace has one span without a
    /// parent, so ordering Root Spans by <c>start_time DESC</c> orders traces by when they began —
    /// and `ix_spans_roots` already stands in that order, so the LIMIT stops the scan.
    /// </summary>
    private static async Task<string[]> SelectCandidatesAsync(
        NpgsqlDataSource dataSource,
        Guid[]? serviceIds,
        TimeFilter timeFilter,
        IReadOnlyList<AttributeFilter> attributeFilters,
        IReadOnlyList<AttributeFilter> resourceFilters,
        int wanted,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand();
        var conditions = new List<string> { "parent_span_id IS NULL" };

        if (serviceIds is not null)
        {
            conditions.Add("service_id = ANY(@services)");
            command.Parameters.AddWithValue("services", serviceIds);
        }

        timeFilter.AddConditions(conditions, command, "start_time");

        if (attributeFilters.Count > 0 || resourceFilters.Count > 0)
        {
            // Same-span semantics (ADR 0001): one span must satisfy every filter at once. The filter
            // chooses which traces qualify, so it belongs to the draw and not to the aggregation.
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

        command.CommandText = $"""
            SELECT trace_id
            FROM telemetry.spans
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY start_time DESC
            LIMIT {wanted}
            """;

        var traceIds = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            traceIds.Add(reader.GetString(0));
        }

        return traceIds.ToArray();
    }

    /// <summary>
    /// Aggregates the drawn traces and nothing else, reached through `ix_spans_trace_id`. The Source
    /// Filter and the Time Filter stay on the rows: a summary counts the spans the reader is allowed
    /// to see inside the window they asked about, which is what the one-step query counted too.
    /// </summary>
    private static async Task<List<TraceSummaryDto>> SummarizeAsync(
        NpgsqlDataSource dataSource,
        Guid[]? serviceIds,
        TimeFilter timeFilter,
        string[] candidates,
        bool errorsOnly,
        int take,
        CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand();
        var conditions = new List<string> { "trace_id = ANY(@traces)" };
        command.Parameters.AddWithValue("traces", candidates);

        if (serviceIds is not null)
        {
            conditions.Add("service_id = ANY(@services)");
            command.Parameters.AddWithValue("services", serviceIds);
        }

        timeFilter.AddConditions(conditions, command, "start_time");

        var having = errorsOnly ? " HAVING bool_or(status_code = 2)" : "";
        command.CommandText = $"""
            SELECT trace_id,
                   min(start_time) AS start_time,
                   (EXTRACT(EPOCH FROM (max(end_time) - min(start_time))) * 1000)::double precision AS duration_ms,
                   count(*)::int AS span_count,
                   bool_or(status_code = 2) AS has_error,
                   (array_agg(name ORDER BY (parent_span_id IS NULL) DESC, start_time))[1] AS root_name,
                   (array_agg(service_id ORDER BY (parent_span_id IS NULL) DESC, start_time))[1] AS root_service_id
            FROM telemetry.spans
            WHERE {string.Join(" AND ", conditions)}
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

        return items;
    }
}
