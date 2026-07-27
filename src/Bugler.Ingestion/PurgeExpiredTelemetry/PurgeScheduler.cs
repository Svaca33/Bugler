using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bugler.Ingestion.PurgeExpiredTelemetry;

internal sealed class PurgeScheduler(
    TelemetryPurger purger,
    IOptions<IngestionOptions> options,
    ILogger<PurgeScheduler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(options.Value.PurgeIntervalMinutes);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await purger.PurgeOnceAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Telemetry purge run failed; retrying next interval");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
