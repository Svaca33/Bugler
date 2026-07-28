using Bugler.Exploration.Scoping;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Npgsql;

namespace Bugler.Exploration.ObservedKeys;

public sealed record ObservedKeyDto(string Scope, IReadOnlyList<string> Path);

public sealed record ObservedKeysResponse(IReadOnlyList<ObservedKeyDto> Items);

internal static class ObservedKeysEndpoint
{
    private const int SampleSize = 2000;

    public static Task<IResult> HandleLogs(
        Guid? applicationId,
        [FromQuery(Name = "namespace")] string? serviceNamespace,
        string? environment,
        string? service,
        ScopeResolver scope,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken) =>
        Handle(
            "telemetry.log_records", "timestamp",
            SourceFilter.FromQuery(applicationId, serviceNamespace, environment, service),
            scope, dataSource, cancellationToken);

    public static Task<IResult> HandleTraces(
        Guid? applicationId,
        [FromQuery(Name = "namespace")] string? serviceNamespace,
        string? environment,
        string? service,
        ScopeResolver scope,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken) =>
        Handle(
            "telemetry.spans", "start_time",
            SourceFilter.FromQuery(applicationId, serviceNamespace, environment, service),
            scope, dataSource, cancellationToken);

    private static async Task<IResult> Handle(
        string table,
        string timeColumn,
        SourceFilter filter,
        ScopeResolver scope,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        var serviceIds = await scope.ResolveServiceIdsAsync(filter, cancellationToken);

        if (serviceIds is { Length: 0 })
        {
            return TypedResults.Ok(new ObservedKeysResponse([]));
        }

        await using var command = dataSource.CreateCommand();
        var where = "";
        if (serviceIds is not null)
        {
            where = " WHERE service_id = ANY(@services)";
            command.Parameters.AddWithValue("services", serviceIds);
        }

        // Observed Keys are a sample, not a schema: scalar leaf paths from the most recent rows only.
        command.CommandText = $"""
            WITH RECURSIVE sample AS (
                SELECT attributes, resource_attributes
                FROM {table}{where}
                ORDER BY {timeColumn} DESC, id DESC
                LIMIT {SampleSize}
            ),
            node AS (
                SELECT root.scope, ARRAY[root.key] AS path, root.value
                FROM (
                    SELECT 'attribute' AS scope, e.key, e.value FROM sample, LATERAL jsonb_each(attributes) e
                    UNION ALL
                    SELECT 'resource' AS scope, e.key, e.value FROM sample, LATERAL jsonb_each(resource_attributes) e
                ) root
                UNION ALL
                SELECT n.scope, n.path || e.key, e.value
                FROM node n, LATERAL jsonb_each(n.value) e
                WHERE jsonb_typeof(n.value) = 'object'
            )
            SELECT DISTINCT scope, path
            FROM node
            WHERE jsonb_typeof(value) IN ('string', 'number', 'boolean')
            ORDER BY scope, path
            """;

        var items = new List<ObservedKeyDto>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new ObservedKeyDto(reader.GetString(0), reader.GetFieldValue<string[]>(1)));
        }

        return TypedResults.Ok(new ObservedKeysResponse(items));
    }
}
