using System.ComponentModel;
using System.Text.Json;
using Bugler.Access.Contracts;
using Bugler.Exploration.BrowseCatalog;
using Bugler.Exploration.Querying;
using Bugler.Exploration.Scoping;
using Bugler.Registry.Contracts;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Npgsql;

namespace Bugler.Exploration.Mcp;

/// <summary>
/// What Exploration answers at the machine door (ADR 0031). These are not the REST endpoints in
/// another wrapper: the shapes are budgeted in tokens rather than screenfuls, the descriptions are
/// the glossary's own sentences because that is the prose a model needs to use them correctly, and
/// nothing is ever truncated in silence — every answer says how many records the Filter matched.
///
/// Everything here is read-only and passes through <see cref="ScopeResolver"/>, so a Machine Delegation
/// sees exactly what its User sees and no more (ADR 0029).
/// </summary>
[McpServerToolType]
public sealed class ExplorationTools
{
    /// <summary>
    /// Fewer than the UI's hundred: the unit of cost here is a context window, not a screen, and
    /// fifty Log Records is already a great deal of it.
    /// </summary>
    private const int DefaultLimit = 50;

    private const int MaxLimit = 200;

    /// <summary>Where counting stops, exactly as the REST count does: past it the answer reads "more than this".</summary>
    private const int CountCap = 1000;

    private const string Columns =
        "id, service_id, timestamp, severity_number, severity_text, body, trace_id, span_id, " +
        "scope_name, resource_attributes::text, attributes::text";

    [McpServerTool(Name = "browse_catalog", ReadOnly = true)]
    [Description(
        "The Applications and Services whose telemetry this caller may read. A Service is a " +
        "registered sender identified by its Service Namespace (which deployment, usually the " +
        "customer), its Environment (production, staging) and its Service Name (backend, mobile). " +
        "Every other tool addresses telemetry by these facets, so start here.")]
    public static async Task<CatalogResponse> BrowseCatalogAsync(
        IReadVisibility visibility,
        ICatalogReader catalog,
        CancellationToken cancellationToken) =>
        await CatalogEndpoint.Handle(visibility, catalog, cancellationToken);

    [McpServerTool(Name = "search_log_records", ReadOnly = true)]
    [Description(
        "Search stored Log Records, newest first. Every criterion is optional and they combine " +
        "with AND. The Source Filter (application_id, service_namespace, environment, " +
        "service_name) matches registered facts, so it cannot be fooled by what telemetry claims " +
        "about itself. The answer always reports how many records matched, which may be more than " +
        "it returns — narrow the Filter rather than paging.")]
    public static async Task<LogSearchAnswer> SearchLogRecordsAsync(
        ScopeResolver scope,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken,
        [Description("Only this Application's telemetry.")] Guid? applicationId = null,
        [Description("Only this Service Namespace — which deployment the sender belongs to.")]
        string? serviceNamespace = null,
        [Description("Only this Environment, e.g. production or staging.")]
        string? environment = null,
        [Description("Only this Service Name, e.g. backend or mobile.")]
        string? serviceName = null,
        [Description("At least this Severity Band: Error, Warn, Info or Debug. Error includes FATAL.")]
        string? severityBand = null,
        [Description("A duration back from now as ISO-8601 — PT15M, PT1H, P7D. A week is 7 days and a month 30.")]
        string? within = null,
        [Description("Lower bound as an ISO-8601 instant with its offset. Use instead of 'within'.")]
        string? from = null,
        [Description("Upper bound as an ISO-8601 instant with its offset.")]
        string? to = null,
        [Description("Case-insensitive substring of the Log Record's body.")]
        string? bodyContains = null,
        [Description("Only Log Records belonging to this trace id.")]
        string? traceId = null,
        [Description("Attribute Filters on the Log Record's own attributes, each written key=value.")]
        string[]? attributes = null,
        [Description("Attribute Filters on Resource Attributes — the entity that emitted the record.")]
        string[]? resourceAttributes = null,
        [Description("How many to return. Default 50, at most 200.")]
        int? limit = null)
    {
        // A refused Filter is raised rather than answered: an empty list with an explanation beside
        // it reads, to whoever asked, exactly like "nothing is wrong" (ADR 0031).
        var severityMin = ReadBand(severityBand);

        if (!LogCriteria.TryCreate(
                applicationId, serviceNamespace, environment, serviceName, severityMin,
                ParseWithin(within), ParseBound(from), ParseBound(to),
                bodyContains, traceId, attributes, resourceAttributes,
                out var criteria, out var error))
        {
            throw new McpException(error ?? "The filter could not be read.");
        }

        var serviceIds = await scope.ResolveServiceIdsAsync(criteria.Source, cancellationToken);
        if (serviceIds is { Length: 0 })
        {
            // Not a refusal and not an absence of trouble either — say which it is.
            return new LogSearchAnswer([], 0, false, NothingVisible);
        }

        var take = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var named = Named(attributes, resourceAttributes);

        await using var command = dataSource.CreateCommand();
        var where = criteria.Where(command, serviceIds);
        command.CommandText =
            $"SELECT {Columns} FROM telemetry.log_records{where} ORDER BY timestamp DESC, id DESC LIMIT {take}";

        var items = new List<LogRecordSummary>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(ReadSummary(reader, named));
            }
        }

        var (matched, capped) = await CountAsync(dataSource, criteria, serviceIds, cancellationToken);
        return new LogSearchAnswer(items, matched, capped, Note(items.Count, matched, capped));
    }

    [McpServerTool(Name = "get_log_record", ReadOnly = true)]
    [Description(
        "One Log Record whole, with every attribute it carries — the answer to 'what else was on " +
        "that record', which the search deliberately leaves out.")]
    public static async Task<LogRecordDetail?> GetLogRecordAsync(
        [Description("The id as search_log_records reported it.")] long id,
        ScopeResolver scope,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        var serviceIds = await scope.ResolveServiceIdsAsync(SourceFilter.None, cancellationToken);
        if (serviceIds is { Length: 0 })
        {
            return null;
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
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new LogRecordDetail(
            reader.GetInt64(0),
            reader.GetGuid(1),
            reader.GetDateTime(2),
            SeverityBand.Of(reader.GetInt16(3)),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetString(8),
            Sql.ParseJson(reader.GetString(9)),
            Sql.ParseJson(reader.GetString(10)));
    }

    [McpServerTool(Name = "list_observed_keys", ReadOnly = true)]
    [Description(
        "The attribute keys present in a recent sample of stored telemetry — what an Attribute " +
        "Filter can be built from. A sample, not a schema: a key that only rare records carry may " +
        "be missing. Ask before guessing at key names, because a wrong key matches nothing and " +
        "looks exactly like an absence of trouble.")]
    public static async Task<ObservedKeysAnswer> ListObservedKeysAsync(
        ScopeResolver scope,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken,
        [Description("Which signal to sample: 'logs' or 'traces'. Defaults to logs.")]
        string? signal = null,
        Guid? applicationId = null,
        string? serviceNamespace = null,
        string? environment = null,
        string? serviceName = null)
    {
        var wantsTraces = string.Equals(signal, "traces", StringComparison.OrdinalIgnoreCase);
        var filter = SourceFilter.FromQuery(applicationId, serviceNamespace, environment, serviceName);
        var keys = await ObservedKeys.ObservedKeysReader.ReadAsync(
            wantsTraces ? "telemetry.spans" : "telemetry.log_records",
            wantsTraces ? "start_time" : "timestamp",
            filter, scope, dataSource, cancellationToken);

        return new ObservedKeysAnswer(keys, SampleNote);
    }

    [McpServerTool(Name = "list_releases", ReadOnly = true)]
    [Description(
        "When each Service began reporting a version it was not already running. The cheapest " +
        "answer to 'did this start after a deploy': compare a Release's instant with when trouble " +
        "began. Releases are not bounded by retention, so old ones are still here.")]
    public static async Task<ReleasesAnswer> ListReleasesAsync(
        ScopeResolver scope,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken,
        Guid? applicationId = null,
        string? serviceNamespace = null,
        string? environment = null,
        string? serviceName = null,
        [Description("A duration back from now as ISO-8601 — PT1H, P7D, P30D. Defaults to the last 7 days.")]
        string? within = null,
        [Description("How many to return, newest last. Default 50, at most 200.")]
        int? limit = null)
    {
        var filter = SourceFilter.FromQuery(applicationId, serviceNamespace, environment, serviceName);
        var serviceIds = await scope.ResolveServiceIdsAsync(filter, cancellationToken);
        if (serviceIds is { Length: 0 })
        {
            return new ReleasesAnswer([], NothingVisible);
        }

        if (!TimeFilter.TryCreate(
                ParseWithin(within) ?? ParseWithin("P7D"), null, null, out var time, out var error))
        {
            throw new McpException(error ?? "The time filter could not be read.");
        }

        var take = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);

        await using var command = dataSource.CreateCommand();
        command.CommandTimeout = Sql.AggregateTimeoutSeconds;

        var conditions = new List<string> { "previous_version IS NOT NULL" };
        if (serviceIds is not null)
        {
            conditions.Add("service_id = ANY(@services)");
            command.Parameters.AddWithValue("services", serviceIds);
        }

        time.AddConditions(conditions, command, "observed_at");
        command.CommandText = $"""
            SELECT service_id, version, previous_version, observed_at
            FROM telemetry.releases
            WHERE {string.Join(" AND ", conditions)}
            ORDER BY observed_at DESC, id DESC
            LIMIT {take}
            """;

        var releases = new List<ReleaseMark>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            releases.Add(new ReleaseMark(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetDateTime(3)));
        }

        releases.Reverse();
        return new ReleasesAnswer(
            releases,
            releases.Count == take
                ? $"Returned the {take} most recent releases in that window; there may be older ones."
                : null);
    }

    [McpServerTool(Name = "get_trace", ReadOnly = true)]
    [Description(
        "Every Span sharing one trace id — a single request's journey through one or more " +
        "Services. Flat rather than nested: each Span names its parent, so the shape is yours to " +
        "rebuild. Reach a trace id from a Log Record's trace_id.")]
    public static async Task<TraceAnswer?> GetTraceAsync(
        [Description("The trace id, as it appears on a Log Record.")] string traceId,
        ScopeResolver scope,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken,
        [Description("How many Spans to return, earliest first. Default 50, at most 200.")]
        int? limit = null)
    {
        var normalized = traceId?.Trim().ToLowerInvariant() ?? "";
        if (normalized.Length == 0)
        {
            throw new McpException("A trace id is required.");
        }

        var serviceIds = await scope.ResolveServiceIdsAsync(SourceFilter.None, cancellationToken);
        if (serviceIds is { Length: 0 })
        {
            return null;
        }

        var take = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);

        await using var command = dataSource.CreateCommand();
        var scopeFilter = serviceIds is null ? "" : " AND service_id = ANY(@services)";
        if (serviceIds is not null)
        {
            command.Parameters.AddWithValue("services", serviceIds);
        }

        command.Parameters.AddWithValue("traceId", normalized);
        command.CommandText = $"""
            SELECT span_id, parent_span_id, service_id, name, start_time, end_time,
                   status_code, status_message, count(*) OVER ()::int AS total
            FROM telemetry.spans
            WHERE trace_id = @traceId{scopeFilter}
            ORDER BY start_time, id
            LIMIT {take}
            """;

        var spans = new List<TraceSpan>();
        var total = 0;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var start = reader.GetDateTime(4);
            spans.Add(new TraceSpan(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.GetGuid(2),
                reader.GetString(3),
                start,
                (reader.GetDateTime(5) - start).TotalMilliseconds,
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7)));
            total = reader.GetInt32(8);
        }

        if (spans.Count == 0)
        {
            return null;
        }

        return new TraceAnswer(
            normalized,
            spans,
            total,
            spans.Count < total
                ? $"Returned the first {spans.Count} of {total} spans, in start order. The rest " +
                  "were not read — this is not the whole journey."
                : null);
    }

    private const string NothingVisible =
        "Nothing was searched: this machine delegation can read no service matching that source " +
        "filter.";

    private const string SampleNote =
        "These keys are a sample of recent telemetry, not a schema — an absent key does not prove " +
        "no record carries it.";

    private static string? Note(int returned, int matched, bool capped)
    {
        if (!capped && returned >= matched)
        {
            return null;
        }

        var total = capped ? $"more than {CountCap}" : matched.ToString();
        return $"Returned {returned} of {total} matching log records — the newest ones. " +
               "The rest were not read; narrow the filter rather than assuming these are all of them.";
    }

    private static async Task<(int Matched, bool Capped)> CountAsync(
        NpgsqlDataSource dataSource,
        LogCriteria criteria,
        Guid[]? serviceIds,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var command = dataSource.CreateCommand();
            command.CommandTimeout = Sql.AggregateTimeoutSeconds;
            var where = criteria.Where(command, serviceIds);
            command.CommandText =
                $"SELECT count(*)::int FROM (SELECT 1 FROM telemetry.log_records{where} LIMIT {CountCap + 1}) matched";

            var counted = (int)(await command.ExecuteScalarAsync(cancellationToken))!;
            return counted > CountCap ? (CountCap, true) : (counted, false);
        }
        catch (NpgsqlException) when (!cancellationToken.IsCancellationRequested)
        {
            // The page stands; only the total is missing, and saying -1 would read as a number.
            return (-1, false);
        }
    }

    private static short? ReadBand(string? band)
    {
        if (string.IsNullOrWhiteSpace(band))
        {
            return null;
        }

        return SeverityBand.Floor(band)
               ?? throw new McpException(
                   $"'{band}' is not a Severity Band. Use one of: {string.Join(", ", SeverityBand.Names)}.");
    }

    private static RelativeRange? ParseWithin(string? within) =>
        RelativeRange.TryParse(within, null, out var parsed) ? parsed : null;

    private static RangeBound? ParseBound(string? bound) =>
        RangeBound.TryParse(bound, null, out var parsed) ? parsed : null;

    /// <summary>
    /// The attribute keys the caller named in a Filter. They come back on every row because they
    /// are why the row matched — everything else stays behind get_log_record, where one record's
    /// worth of `telemetry.sdk.*` costs nothing.
    /// </summary>
    private static string[] Named(string[]? attributes, string[]? resourceAttributes) =>
        [.. (attributes ?? []).Concat(resourceAttributes ?? [])
            .Select(pair => pair.Split('=', 2)[0])
            .Where(key => key.Length > 0)
            .Distinct()];

    private static LogRecordSummary ReadSummary(NpgsqlDataReader reader, string[] named)
    {
        var attributes = Sql.ParseJson(reader.GetString(10));
        var resource = Sql.ParseJson(reader.GetString(9));

        return new LogRecordSummary(
            reader.GetInt64(0),
            reader.GetGuid(1),
            reader.GetDateTime(2),
            SeverityBand.Of(reader.GetInt16(3)),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6),
            named.Length == 0 ? null : Pick(named, attributes, resource));
    }

    private static Dictionary<string, string>? Pick(
        string[] named, JsonElement attributes, JsonElement resource)
    {
        var picked = new Dictionary<string, string>();
        foreach (var key in named)
        {
            if (TryRead(attributes, key, out var value) || TryRead(resource, key, out value))
            {
                picked[key] = value;
            }
        }

        return picked.Count == 0 ? null : picked;
    }

    private static bool TryRead(JsonElement source, string path, out string value)
    {
        value = "";
        if (source.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var current = source;
        foreach (var segment in path.Split('.'))
        {
            if (current.ValueKind != JsonValueKind.Object
                || !current.TryGetProperty(segment, out current))
            {
                return false;
            }
        }

        value = current.ValueKind == JsonValueKind.String
            ? current.GetString() ?? ""
            : current.ToString();
        return true;
    }
}
