using Bugler.Alerting.Episodes;
using Bugler.Alerting.Settings;
using Bugler.Registry.Contracts;
using Bugler.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bugler.Alerting.CloseQuietEpisodes;

/// <summary>
/// One closing sweep, and nothing but state: an open Episode whose Service stayed quiet for its
/// Quiet Window becomes Quieted; one whose Sensitivity turned Off becomes Muted (the net for a
/// detect run racing a settings change). Nobody is notified — the Alert is the only message
/// Bugler sends (ADR 0003). One transaction.
/// </summary>
public sealed class EpisodeCloser(
    IServiceScopeFactory scopeFactory,
    ILogger<EpisodeCloser> logger)
{
    public async Task CloseOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AlertingDbContext>();

        var openEpisodes = await dbContext.Episodes
            .Where(e => e.ClosedAt == null)
            .ToListAsync(cancellationToken);
        if (openEpisodes.Count == 0)
        {
            return;
        }

        var catalog = await scope.ServiceProvider.GetRequiredService<ICatalogReader>()
            .GetServicesAsync(cancellationToken);
        var effective = EffectiveSettings.Build(
            catalog,
            await dbContext.ApplicationSettings.AsNoTracking().ToListAsync(cancellationToken),
            await dbContext.ServiceSettings.AsNoTracking().ToListAsync(cancellationToken),
            await dbContext.FingerprintQuietWindows.AsNoTracking().ToListAsync(cancellationToken));

        var now = DateTimeOffset.UtcNow;
        var mutedServices = new List<ServiceId>();

        foreach (var episode in openEpisodes)
        {
            var reason = CloseDecision.Decide(
                effective.SensitivityOf(episode.ServiceId),
                episode.LastMatchAt,
                effective.QuietWindowOf(episode.ServiceId, episode.Fingerprint),
                now);

            switch (reason)
            {
                case EpisodeCloseReason.SensitivityOff:
                    mutedServices.Add(episode.ServiceId); // SilentClose closes and lapses below.
                    break;

                case EpisodeCloseReason.QuietWindow:
                    episode.ClosedAt = now;
                    episode.CloseReason = EpisodeCloseReason.QuietWindow;
                    logger.LogInformation(
                        "Episode of service {ServiceId} fell quiet after {Errors} errors "
                        + "and {Warns} warnings",
                        episode.ServiceId, episode.ErrorCount, episode.WarnCount);
                    break;
            }
        }

        await SilentClose.ApplyAsync(dbContext, mutedServices, now, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
