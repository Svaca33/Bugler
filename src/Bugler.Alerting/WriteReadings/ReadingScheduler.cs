using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bugler.Alerting.WriteReadings;

/// <summary>
/// The Readings' own loop, apart from the main beat on purpose: a completion may take a couple
/// of minutes on a slow model, and probing, detection and delivery must not stand behind it —
/// a stalled provider must cost Bugler nothing but unexplained Alerts (Alerting ADR 0009).
/// </summary>
internal sealed class ReadingScheduler(
    ReadingWriter writer,
    IOptions<AlertingOptions> options,
    ILogger<ReadingScheduler> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(options.Value.PollIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await writer.WriteOnceAsync(stoppingToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Reading writing run failed; retrying next interval");
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
