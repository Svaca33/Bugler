using Bugler.SharedKernel;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Bugler.Ingestion.EraseDeletedServiceTelemetry;

/// <summary>
/// Erases the Signals of Services that were deleted from the Catalog. Unlike a Purge, this is
/// not driven by retention: the Service is gone, so everything it sent goes with it (ADR 0007).
/// Deleting by Service is idempotent, as at-least-once delivery requires.
/// </summary>
internal sealed class DeletedServiceEraser(
    NpgsqlDataSource dataSource,
    ILogger<DeletedServiceEraser> logger) : IIntegrationEventHandler<ServicesDeleted>
{
    public async Task HandleAsync(ServicesDeleted integrationEvent, CancellationToken cancellationToken)
    {
        if (integrationEvent.ServiceIds.Count == 0)
        {
            return;
        }

        var serviceIds = integrationEvent.ServiceIds.Select(id => id.Value).ToArray();

        var logs = await EraseAsync(
            "DELETE FROM telemetry.log_records WHERE service_id = ANY(@services)",
            serviceIds, cancellationToken);
        var spans = await EraseAsync(
            "DELETE FROM telemetry.spans WHERE service_id = ANY(@services)",
            serviceIds, cancellationToken);
        // Releases outlive retention (ADR 0016) but not the Service they describe: Deletion takes
        // everything a Service ever sent, and this was observed from it.
        var releases = await EraseAsync(
            "DELETE FROM telemetry.releases WHERE service_id = ANY(@services)",
            serviceIds, cancellationToken);

        logger.LogInformation(
            "Erased {Logs} log records, {Spans} spans and {Releases} releases of {Services} deleted services",
            logs, spans, releases, serviceIds.Length);
    }

    private async Task<int> EraseAsync(string sql, Guid[] serviceIds, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("services", serviceIds);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
