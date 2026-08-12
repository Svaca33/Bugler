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

        var now = DateTimeOffset.UtcNow;
        await LapseWiltedClaimsAsync(dbContext, now, cancellationToken);

        var openEpisodes = await dbContext.Episodes
            .Where(e => e.ClosedAt == null)
            .ToListAsync(cancellationToken);
        if (openEpisodes.Count == 0)
        {
            // The lapses alone are still worth committing.
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var catalog = await scope.ServiceProvider.GetRequiredService<ICatalogReader>()
            .GetServicesAsync(cancellationToken);
        var effective = EffectiveSettings.Build(
            catalog,
            await dbContext.ApplicationSettings.AsNoTracking().ToListAsync(cancellationToken),
            await dbContext.ServiceSettings.AsNoTracking().ToListAsync(cancellationToken),
            await dbContext.FingerprintQuietWindows.AsNoTracking().ToListAsync(cancellationToken));

        var mutedByWatch = new Dictionary<Watch, List<ServiceId>>();

        foreach (var episode in openEpisodes)
        {
            var reason = CloseDecision.Decide(
                IsWatchOff(episode, effective),
                episode.AcknowledgedAt is not null,
                episode.ClaimedByDelegationId is not null,
                episode.LastMatchAt,
                effective.QuietWindowOf(episode.ServiceId, episode.Fingerprint),
                now);

            switch (reason)
            {
                case EpisodeCloseReason.WatchOff:
                    // SilentClose closes and lapses below, per Watch.
                    mutedByWatch.TryAdd(episode.Watch, []);
                    mutedByWatch[episode.Watch].Add(episode.ServiceId);
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

        foreach (var (watch, services) in mutedByWatch)
        {
            await SilentClose.ApplyAsync(dbContext, services, watch, now, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The lease doing its work: claims whose time ran out fall off by themselves, each with its
    /// Journal line, before this run measures any Quiet Window — a crashed agent leaves a wilted
    /// mark, never a zombie Episode. Closed Episodes are swept too: a claim standing on a Muted
    /// one holds nothing, and here is where it stops pretending to.
    /// </summary>
    private static async Task LapseWiltedClaimsAsync(
        AlertingDbContext dbContext, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var wilted = await dbContext.Episodes
            .Where(e => e.ClaimedByDelegationId != null && e.ClaimLeaseUntil < now)
            .ToListAsync(cancellationToken);

        foreach (var episode in wilted)
        {
            if (episode.ShedClaim() is { } shed)
            {
                dbContext.JournalEntries.Add(new JournalEntry
                {
                    EpisodeId = episode.Id,
                    Kind = JournalEntryKind.ClaimLapsed,
                    UserId = shed.UserId,
                    DelegationId = shed.DelegationId,
                    At = now,
                });
            }
        }
    }

    /// <summary>Each Watch has its own switch: Sensitivity for the Logs Watch, an address for the Health Check Watch.</summary>
    private static bool IsWatchOff(Episode episode, EffectiveSettings effective) => episode.Watch switch
    {
        Watch.Logs => effective.SensitivityOf(episode.ServiceId) == Sensitivity.Off,
        Watch.HealthCheck => effective.HealthCheckUrlOf(episode.ServiceId) is null,
        _ => false,
    };
}
