using Bugler.Registry.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Bugler.Ingestion.PurgeExpiredTelemetry;

/// <summary>
/// Permanently removes Signals that outlived their Service's retention.
/// Services are grouped by effective days so each table takes one DELETE per distinct policy.
/// </summary>
public sealed class TelemetryPurger(
    IServiceScopeFactory scopeFactory,
    NpgsqlDataSource dataSource,
    ILogger<TelemetryPurger> logger)
{
    public async Task PurgeOnceAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<ServiceRetention> retentions;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            retentions = await scope.ServiceProvider.GetRequiredService<IRetentionReader>()
                .GetEffectiveRetentionsAsync(cancellationToken);
        }

        foreach (var group in retentions.GroupBy(r => r.Days))
        {
            var cutoff = DateTime.UtcNow.AddDays(-group.Key);
            var serviceIds = group.Select(r => r.ServiceId.Value).ToArray();

            var logs = await DeleteAsync(
                "DELETE FROM telemetry.log_records WHERE service_id = ANY(@services) AND timestamp < @cutoff",
                serviceIds, cutoff, cancellationToken);
            var spans = await DeleteAsync(
                "DELETE FROM telemetry.spans WHERE service_id = ANY(@services) AND start_time < @cutoff",
                serviceIds, cutoff, cancellationToken);

            if (logs + spans > 0)
            {
                logger.LogInformation(
                    "Purged {Logs} log records and {Spans} spans older than {Days} days", logs, spans, group.Key);
            }
        }
    }

    private async Task<int> DeleteAsync(
        string sql, Guid[] serviceIds, DateTime cutoff, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("services", serviceIds);
        command.Parameters.AddWithValue("cutoff", cutoff);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
