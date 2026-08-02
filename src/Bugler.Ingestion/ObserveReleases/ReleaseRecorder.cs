using Bugler.SharedKernel;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Bugler.Ingestion.ObserveReleases;

/// <summary>
/// Turns version sightings into rows of `telemetry.releases`. Owns the <see cref="ReleaseLedger"/>
/// and is the only thread that touches it: the channel has a single reader, so the decision and the
/// write that confirms it can never interleave with another sighting of the same Service.
/// </summary>
internal sealed class ReleaseRecorder(
    ReleaseObservations observations,
    NpgsqlDataSource dataSource,
    IOptions<IngestionOptions> options,
    ILogger<ReleaseRecorder> logger) : BackgroundService
{
    private static readonly TimeSpan RestoreRetryDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var ledger = new ReleaseLedger(options.Value.ReleaseStragglerWindow);
        await RestoreAsync(ledger, stoppingToken);

        try
        {
            await foreach (var sighting in observations.Sightings.ReadAllAsync(stoppingToken))
            {
                await RecordAsync(ledger, sighting, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown. Nothing is drained on the way out: an unrecorded sighting is not lost but
            // deferred — the sender is still running that version and will declare it again.
        }
    }

    private async Task RecordAsync(
        ReleaseLedger ledger, ServiceVersionSighting sighting, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        if (ledger.Admit(sighting, now) is not { } release)
        {
            return;
        }

        try
        {
            await using var command = dataSource.CreateCommand(
                """
                INSERT INTO telemetry.releases
                    (service_id, version, previous_version, observed_at, recorded_at)
                VALUES (@service, @version, @previous, @observed, @recorded)
                """);
            command.Parameters.AddWithValue("service", release.ServiceId.Value);
            command.Parameters.AddWithValue("version", release.Version);
            command.Parameters.AddWithValue("previous", (object?)release.PreviousVersion ?? DBNull.Value);
            command.Parameters.AddWithValue("observed", release.ObservedAt);
            command.Parameters.AddWithValue("recorded", now);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Left uncommitted on purpose: the ledger still holds the old version, so the next
            // block declaring the new one offers the very same Release again.
            logger.LogWarning(
                exception, "Could not record release {Version} of service {ServiceId}",
                release.Version, release.ServiceId.Value);
            return;
        }

        ledger.Commit(release, now);

        if (release.PreviousVersion is not null)
        {
            logger.LogInformation(
                "Service {ServiceId} released {Version}, replacing {PreviousVersion}",
                release.ServiceId.Value, release.Version, release.PreviousVersion);
        }
    }

    /// <summary>
    /// Reads back what each Service was last recorded running. Ordered by the server's clock, not
    /// the sender's: this asks what Bugler decided last, and a sender's clock running backwards
    /// must not be able to make an older decision look like the newer one.
    /// </summary>
    private async Task RestoreAsync(ReleaseLedger ledger, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await using var command = dataSource.CreateCommand(
                    """
                    SELECT DISTINCT ON (service_id) service_id, version, previous_version, recorded_at
                    FROM telemetry.releases
                    ORDER BY service_id, recorded_at DESC, id DESC
                    """);

                var now = DateTime.UtcNow;
                var restored = 0;
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    ledger.Seed(
                        new ServiceId(reader.GetGuid(0)),
                        reader.GetString(1),
                        reader.IsDBNull(2) ? null : reader.GetString(2),
                        reader.GetDateTime(3),
                        now);
                    restored++;
                }

                logger.LogInformation("Restored the declared version of {Services} services", restored);
                return;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Draining before this succeeds would take every Service for unknown and write a
                // second row establishing the version it already knew, so the channel waits.
                logger.LogWarning(exception, "Could not restore declared versions; retrying");
                await Task.Delay(RestoreRetryDelay, cancellationToken);
            }
        }
    }
}
