using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;

namespace Bugler.Ingestion.Storage;

/// <summary>
/// Drains the buffer and persists Batches via binary COPY. A failed flush loses
/// its Batch (bounded loss window per ADR 0003) — it is logged, never retried blindly.
/// </summary>
internal sealed class LogWriter(
    TelemetryBuffer buffer,
    NpgsqlDataSource dataSource,
    IOptions<IngestionOptions> options,
    ILogger<LogWriter> logger) : BackgroundService
{
    private const string CopyCommand =
        "COPY telemetry.log_records (instance_id, timestamp, observed_timestamp, severity_number, " +
        "severity_text, body, trace_id, span_id, service_name, scope_name, resource_attributes, attributes) " +
        "FROM STDIN (FORMAT BINARY)";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var maxBatch = options.Value.MaxBatchSize;
        var batch = new List<LogRecordRow>(maxBatch);

        try
        {
            while (await buffer.Logs.WaitToReadAsync(stoppingToken))
            {
                while (batch.Count < maxBatch && buffer.Logs.TryRead(out var row))
                {
                    batch.Add(row);
                }

                await FlushAsync(batch, stoppingToken);
                batch.Clear();
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown: drain what is already buffered so a clean stop loses nothing.
            while (batch.Count < maxBatch && buffer.Logs.TryRead(out var row))
            {
                batch.Add(row);
            }

            await FlushAsync(batch, CancellationToken.None);
        }
    }

    private async Task FlushAsync(List<LogRecordRow> batch, CancellationToken cancellationToken)
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
                await importer.WriteAsync(row.Timestamp, NpgsqlDbType.TimestampTz, cancellationToken);
                await WriteNullableAsync(importer, row.ObservedTimestamp, NpgsqlDbType.TimestampTz, cancellationToken);
                await importer.WriteAsync(row.SeverityNumber, NpgsqlDbType.Smallint, cancellationToken);
                await WriteNullableAsync(importer, row.SeverityText, NpgsqlDbType.Text, cancellationToken);
                await WriteNullableAsync(importer, row.Body, NpgsqlDbType.Text, cancellationToken);
                await WriteNullableAsync(importer, row.TraceId, NpgsqlDbType.Text, cancellationToken);
                await WriteNullableAsync(importer, row.SpanId, NpgsqlDbType.Text, cancellationToken);
                await WriteNullableAsync(importer, row.ServiceName, NpgsqlDbType.Text, cancellationToken);
                await WriteNullableAsync(importer, row.ScopeName, NpgsqlDbType.Text, cancellationToken);
                await importer.WriteAsync(row.ResourceAttributes, NpgsqlDbType.Jsonb, cancellationToken);
                await importer.WriteAsync(row.Attributes, NpgsqlDbType.Jsonb, cancellationToken);
            }

            await importer.CompleteAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Failed to persist a batch of {Count} log records; batch lost", batch.Count);
        }
    }

    private static async Task WriteNullableAsync<T>(
        NpgsqlBinaryImporter importer, T? value, NpgsqlDbType type, CancellationToken cancellationToken)
    {
        if (value is null)
        {
            await importer.WriteNullAsync(cancellationToken);
        }
        else
        {
            await importer.WriteAsync(value, type, cancellationToken);
        }
    }
}
