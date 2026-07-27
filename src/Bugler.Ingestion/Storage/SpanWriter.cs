using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;

namespace Bugler.Ingestion.Storage;

/// <summary>
/// Drains the span buffer and persists Batches via binary COPY — the trace-side
/// counterpart of <see cref="LogWriter"/>, with the same loss semantics (ADR 0003).
/// </summary>
internal sealed class SpanWriter(
    TelemetryBuffer buffer,
    NpgsqlDataSource dataSource,
    IOptions<IngestionOptions> options,
    ILogger<SpanWriter> logger) : BackgroundService
{
    private const string CopyCommand =
        "COPY telemetry.spans (instance_id, trace_id, span_id, parent_span_id, name, kind, " +
        "start_time, end_time, status_code, status_message, service_name, scope_name, " +
        "resource_attributes, attributes, events, links) FROM STDIN (FORMAT BINARY)";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var maxBatch = options.Value.MaxBatchSize;
        var batch = new List<SpanRow>(maxBatch);

        try
        {
            while (await buffer.Spans.WaitToReadAsync(stoppingToken))
            {
                while (batch.Count < maxBatch && buffer.Spans.TryRead(out var row))
                {
                    batch.Add(row);
                }

                await FlushAsync(batch, stoppingToken);
                batch.Clear();
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            while (batch.Count < maxBatch && buffer.Spans.TryRead(out var row))
            {
                batch.Add(row);
            }

            await FlushAsync(batch, CancellationToken.None);
        }
    }

    private async Task FlushAsync(List<SpanRow> batch, CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return;
        }

        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var importer = await connection.BeginBinaryImportAsync(CopyCommand, cancellationToken);

            foreach (var row in batch)
            {
                await importer.StartRowAsync(cancellationToken);
                await importer.WriteAsync(row.InstanceId, NpgsqlDbType.Uuid, cancellationToken);
                await importer.WriteAsync(row.TraceId, NpgsqlDbType.Text, cancellationToken);
                await importer.WriteAsync(row.SpanId, NpgsqlDbType.Text, cancellationToken);
                await WriteNullableAsync(importer, row.ParentSpanId, cancellationToken);
                await importer.WriteAsync(row.Name, NpgsqlDbType.Text, cancellationToken);
                await importer.WriteAsync(row.Kind, NpgsqlDbType.Smallint, cancellationToken);
                await importer.WriteAsync(row.StartTime, NpgsqlDbType.TimestampTz, cancellationToken);
                await importer.WriteAsync(row.EndTime, NpgsqlDbType.TimestampTz, cancellationToken);
                await importer.WriteAsync(row.StatusCode, NpgsqlDbType.Smallint, cancellationToken);
                await WriteNullableAsync(importer, row.StatusMessage, cancellationToken);
                await WriteNullableAsync(importer, row.ServiceName, cancellationToken);
                await WriteNullableAsync(importer, row.ScopeName, cancellationToken);
                await importer.WriteAsync(row.ResourceAttributes, NpgsqlDbType.Jsonb, cancellationToken);
                await importer.WriteAsync(row.Attributes, NpgsqlDbType.Jsonb, cancellationToken);
                await importer.WriteAsync(row.Events, NpgsqlDbType.Jsonb, cancellationToken);
                await importer.WriteAsync(row.Links, NpgsqlDbType.Jsonb, cancellationToken);
            }

            await importer.CompleteAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Failed to persist a batch of {Count} spans; batch lost", batch.Count);
        }
    }

    private static async Task WriteNullableAsync(
        NpgsqlBinaryImporter importer, string? value, CancellationToken cancellationToken)
    {
        if (value is null)
        {
            await importer.WriteNullAsync(cancellationToken);
        }
        else
        {
            await importer.WriteAsync(value, NpgsqlDbType.Text, cancellationToken);
        }
    }
}
