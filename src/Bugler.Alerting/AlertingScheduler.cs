using Bugler.Alerting.CloseQuietEpisodes;
using Bugler.Alerting.DeliverMessages;
using Bugler.Alerting.DetectEpisodes;
using Bugler.Alerting.WatchHealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bugler.Alerting;

/// <summary>
/// The one loop ADR 0010 prescribes, now with a watch that asks rather than reads: probe, detect,
/// close, deliver, sleep. Each unit fails alone — a mail outage must not stop Episodes from
/// closing, nor the other way round.
///
/// The order is not arbitrary. Probing runs before closing so a failure this beat lands as a
/// match before the Quiet Window is measured, and before delivering so an Episode opened by a
/// probe sends its Alert in the same beat rather than the next.
/// </summary>
internal sealed class AlertingScheduler(
    HealthCheckWatcher healthChecks,
    EpisodeDetector detector,
    EpisodeCloser closer,
    DeliveryRunner runner,
    IOptions<AlertingOptions> options,
    ILogger<AlertingScheduler> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(options.Value.PollIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunQuietlyAsync(healthChecks.WatchOnceAsync, "health checks", stoppingToken);
            await RunQuietlyAsync(detector.DetectOnceAsync, "detection", stoppingToken);
            await RunQuietlyAsync(closer.CloseOnceAsync, "closing", stoppingToken);
            await RunQuietlyAsync(runner.DeliverOnceAsync, "delivery", stoppingToken);

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

    private async Task RunQuietlyAsync(
        Func<CancellationToken, Task> unit, string name, CancellationToken stoppingToken)
    {
        try
        {
            await unit(stoppingToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Alerting {Unit} run failed; retrying next interval", name);
        }
    }
}
