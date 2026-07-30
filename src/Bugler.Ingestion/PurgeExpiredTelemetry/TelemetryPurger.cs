using Bugler.Registry.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Bugler.Ingestion.PurgeExpiredTelemetry;

/// <summary>
/// Permanently removes Signals that outlived their Service's retention.
/// Services are grouped by effective days so each table takes one DELETE per distinct policy.
/// </summary>
public sealed class TelemetryPurger(
    IServiceScopeFactory scopeFactory,
    NpgsqlDataSource dataSource,
    IOptions<IngestionOptions> options,
    ILogger<TelemetryPurger> logger)
{
    public async Task PurgeOnceAsync(CancellationToken cancellationToken)
    {
        // Taken before the read, so telemetry of a Service registered while we read it is
        // always newer than this and can never be mistaken for an orphan.
        var catalogReadAt = DateTime.UtcNow;

        IReadOnlyList<ServiceRetention> retentions;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            retentions = await scope.ServiceProvider.GetRequiredService<IRetentionReader>()
                .GetEffectiveRetentionsAsync(cancellationToken);
        }

        // Logs and spans run on their own clocks, so each table is grouped by its own number.
        foreach (var group in retentions.GroupBy(r => r.LogDays))
        {
            var logs = await DeleteAsync(
                "telemetry.log_records", "service_id = ANY(@services) AND timestamp < @cutoff",
                group.Select(r => r.ServiceId.Value).ToArray(),
                DateTime.UtcNow.AddDays(-group.Key), cancellationToken);

            if (logs > 0)
            {
                logger.LogInformation(
                    "Purged {Logs} log records older than {Days} days", logs, group.Key);
            }
        }

        foreach (var group in retentions.GroupBy(r => r.TraceDays))
        {
            var spans = await DeleteAsync(
                "telemetry.spans", "service_id = ANY(@services) AND start_time < @cutoff",
                group.Select(r => r.ServiceId.Value).ToArray(),
                DateTime.UtcNow.AddDays(-group.Key), cancellationToken);

            if (spans > 0)
            {
                logger.LogInformation(
                    "Purged {Spans} spans older than {Days} days", spans, group.Key);
            }
        }

        await ReclaimOrphansAsync(retentions, catalogReadAt, cancellationToken);
    }

    /// <summary>
    /// Removes telemetry of Services no longer in the Catalog. Erasure on Deletion normally
    /// gets there first; this catches what it cannot see — Signals buffered before the Deletion
    /// and written after the erasure ran, and anything a parked outbox message left behind.
    /// </summary>
    private async Task ReclaimOrphansAsync(
        IReadOnlyList<ServiceRetention> retentions,
        DateTime catalogReadAt,
        CancellationToken cancellationToken)
    {
        var knownServices = retentions.Select(r => r.ServiceId.Value).ToArray();

        var logs = await DeleteAsync(
            "telemetry.log_records", "service_id <> ALL(@services) AND timestamp < @cutoff",
            knownServices, catalogReadAt, cancellationToken);
        var spans = await DeleteAsync(
            "telemetry.spans", "service_id <> ALL(@services) AND start_time < @cutoff",
            knownServices, catalogReadAt, cancellationToken);

        if (logs + spans > 0)
        {
            logger.LogInformation(
                "Reclaimed {Logs} log records and {Spans} spans of services no longer registered", logs, spans);
        }
    }

    /// <summary>
    /// Removes what the predicate matches, a bounded batch at a time. An hour of expired telemetry
    /// would go in one statement comfortably; a backlog would not. A Service whose retention was
    /// just shortened, or one that flooded, puts millions of rows in the way — and as a single
    /// transaction that is minutes of held resources that roll back entirely if anything gives,
    /// leaving the next run to attempt the very same doomed statement. Batching lets every run keep
    /// the ground it gained, so the backlog drains even when no single pass can finish it.
    /// </summary>
    private async Task<int> DeleteAsync(
        string table, string predicate, Guid[] serviceIds, DateTime cutoff, CancellationToken cancellationToken)
    {
        var sql = $"DELETE FROM {table} WHERE ctid IN "
                  + $"(SELECT ctid FROM {table} WHERE {predicate} LIMIT @batch)";
        var batchSize = options.Value.PurgeBatchSize;
        var deleted = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            await using var command = dataSource.CreateCommand(sql);
            command.Parameters.AddWithValue("services", serviceIds);
            command.Parameters.AddWithValue("cutoff", cutoff);
            command.Parameters.AddWithValue("batch", batchSize);

            var removed = await command.ExecuteNonQueryAsync(cancellationToken);
            deleted += removed;

            // A short batch means the predicate has nothing left to give.
            if (removed < batchSize)
            {
                break;
            }
        }

        return deleted;
    }
}
