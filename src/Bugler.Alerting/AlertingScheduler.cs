using Bugler.Alerting.CloseQuietEpisodes;
using Bugler.Alerting.DeliverMessages;
using Bugler.Alerting.DetectEpisodes;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bugler.Alerting;

/// <summary>
/// The one loop ADR 0010 prescribes: detect, close, deliver, sleep. Each unit fails alone — a
/// mail outage must not stop Episodes from closing, nor the other way round.
/// </summary>
internal sealed class AlertingScheduler(
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
