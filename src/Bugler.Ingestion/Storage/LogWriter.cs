using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;

namespace Bugler.Ingestion.Storage;

/// <summary>
/// Drains the buffer and persists Batches via binary COPY. A Batch PostgreSQL refuses is put
/// through a Salvage rather than lost whole (ADR 0020); a Batch that never reached PostgreSQL is
/// lost, and that is the bounded loss window ADR 0003 accepts.
/// </summary>
internal sealed class LogWriter(
    TelemetryBuffer buffer,
    NpgsqlDataSource dataSource,
    IOptions<IngestionOptions> options,
    ILogger<LogWriter> logger) : BackgroundService
{
    /// <summary>Internal so the integration tests can salvage against the real statement.</summary>
    internal const string CopyCommand =
        "COPY telemetry.log_records (service_id, timestamp, observed_timestamp, severity_number, " +
        "severity_text, body, trace_id, span_id, scope_name, resource_attributes, attributes) " +
        "FROM STDIN (FORMAT BINARY)";

    private readonly BatchImporter<LogRecordRow> _importer =
        new(dataSource, CopyCommand, WriteRowAsync, "log records", logger);

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

                await _importer.ImportAsync(batch, stoppingToken);
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

            await _importer.ImportAsync(batch, CancellationToken.None);
        }
    }

    /// <summary>Internal for the same reason as <see cref="CopyCommand"/>: one row, written once.</summary>
    internal static async Task WriteRowAsync(
        NpgsqlBinaryImporter importer, LogRecordRow row, CancellationToken cancellationToken)
    {
        await importer.WriteAsync(row.ServiceId, NpgsqlDbType.Uuid, cancellationToken);
        await importer.WriteAsync(row.Timestamp, NpgsqlDbType.TimestampTz, cancellationToken);
        await WriteNullableAsync(importer, row.ObservedTimestamp, NpgsqlDbType.TimestampTz, cancellationToken);
        await importer.WriteAsync(row.SeverityNumber, NpgsqlDbType.Smallint, cancellationToken);
        await WriteNullableAsync(importer, row.SeverityText, NpgsqlDbType.Text, cancellationToken);
        await WriteNullableAsync(importer, row.Body, NpgsqlDbType.Text, cancellationToken);
        await WriteNullableAsync(importer, row.TraceId, NpgsqlDbType.Text, cancellationToken);
        await WriteNullableAsync(importer, row.SpanId, NpgsqlDbType.Text, cancellationToken);
        await WriteNullableAsync(importer, row.ScopeName, NpgsqlDbType.Text, cancellationToken);
        await importer.WriteAsync(row.ResourceAttributes, NpgsqlDbType.Jsonb, cancellationToken);
        await importer.WriteAsync(row.Attributes, NpgsqlDbType.Jsonb, cancellationToken);
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
