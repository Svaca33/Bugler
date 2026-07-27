using System.Text.Json;
using Bugler.Exploration.Querying;
using Bugler.Exploration.Scoping;
using Bugler.SharedKernel;
using Microsoft.AspNetCore.Http;
using Npgsql;

namespace Bugler.Exploration.SearchLogs;

public sealed record LogRecordDto(
    long Id,
    Guid InstanceId,
    DateTime Timestamp,
    DateTime? ObservedTimestamp,
    short SeverityNumber,
    string? SeverityText,
    string? Body,
    string? TraceId,
    string? SpanId,
    string? ServiceName,
    string? ScopeName,
    JsonElement ResourceAttributes,
    JsonElement Attributes);

public sealed record SearchLogsResponse(IReadOnlyList<LogRecordDto> Items);

internal static class SearchLogsEndpoint
{
    private const string Columns =
        "id, instance_id, timestamp, observed_timestamp, severity_number, severity_text, " +
        "body, trace_id, span_id, service_name, scope_name, resource_attributes::text, attributes::text";

    public static async Task<IResult> Handle(
        Guid? applicationId,
        Guid? instanceId,
        string? tenant,
        short? severityMin,
        DateTime? from,
        DateTime? to,
        string? q,
        string? traceId,
        DateTime? before,
        long? beforeId,
        int? limit,
        ScopeResolver scope,
        NpgsqlDataSource dataSource,
        CancellationToken cancellationToken)
    {
        var instanceIds = await scope.ResolveInstanceIdsAsync(
            applicationId is { } app ? new ApplicationId(app) : null,
            instanceId is { } instance ? new InstanceId(instance) : null,
            cancellationToken);

        if (instanceIds is { Length: 0 })
        {
            return TypedResults.Ok(new SearchLogsResponse([]));
        }

        await using var command = dataSource.CreateCommand();
        var conditions = new List<string>();

        if (instanceIds is not null)
        {
            conditions.Add("instance_id = ANY(@instances)");
            command.Parameters.AddWithValue("instances", instanceIds);
        }

        if (from is { } fromTime)
        {
            conditions.Add("timestamp >= @from");
            command.Parameters.AddWithValue("from", Sql.EnsureUtc(fromTime));
        }

        if (to is { } toTime)
        {
            conditions.Add("timestamp <= @to");
            command.Parameters.AddWithValue("to", Sql.EnsureUtc(toTime));
        }

        if (severityMin is > 0)
        {
            conditions.Add("severity_number >= @severityMin");
            command.Parameters.AddWithValue("severityMin", severityMin.Value);
        }

        if (!string.IsNullOrEmpty(tenant))
        {
            conditions.Add("attributes->>'tenant.id' = @tenant");
            command.Parameters.AddWithValue("tenant", tenant);
        }

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
        var instanceIds = await scope.ResolveInstanceIdsAsync(null, null, cancellationToken);
        if (instanceIds is { Length: 0 })
        {
            return TypedResults.NotFound();
        }

        await using var command = dataSource.CreateCommand();
        var scopeFilter = instanceIds is null ? "" : " AND instance_id = ANY(@instances)";
        if (instanceIds is not null)
        {
            command.Parameters.AddWithValue("instances", instanceIds);
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
        reader.IsDBNull(10) ? null : reader.GetString(10),
        Sql.ParseJson(reader.GetString(11)),
        Sql.ParseJson(reader.GetString(12)));
}
