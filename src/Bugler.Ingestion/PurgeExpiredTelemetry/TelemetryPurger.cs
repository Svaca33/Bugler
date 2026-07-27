using Bugler.Registry.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Bugler.Ingestion.PurgeExpiredTelemetry;

/// <summary>
/// Permanently removes Signals that outlived their Instance's retention.
/// Instances are grouped by effective days so each table takes one DELETE per distinct policy.
/// </summary>
public sealed class TelemetryPurger(
    IServiceScopeFactory scopeFactory,
    NpgsqlDataSource dataSource,
    ILogger<TelemetryPurger> logger)
{
    public async Task PurgeOnceAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<InstanceRetention> retentions;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            retentions = await scope.ServiceProvider.GetRequiredService<IRetentionReader>()
                .GetEffectiveRetentionsAsync(cancellationToken);
        }

        foreach (var group in retentions.GroupBy(r => r.Days))
        {
            var cutoff = DateTime.UtcNow.AddDays(-group.Key);
            var instanceIds = group.Select(r => r.InstanceId.Value).ToArray();

            var logs = await DeleteAsync(
                "DELETE FROM telemetry.log_records WHERE instance_id = ANY(@instances) AND timestamp < @cutoff",
                instanceIds, cutoff, cancellationToken);
            var spans = await DeleteAsync(
                "DELETE FROM telemetry.spans WHERE instance_id = ANY(@instances) AND start_time < @cutoff",
                instanceIds, cutoff, cancellationToken);

            if (logs + spans > 0)
            {
                logger.LogInformation(
                    "Purged {Logs} log records and {Spans} spans older than {Days} days", logs, spans, group.Key);
            }
        }
    }

    private async Task<int> DeleteAsync(
        string sql, Guid[] instanceIds, DateTime cutoff, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("instances", instanceIds);
        command.Parameters.AddWithValue("cutoff", cutoff);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
