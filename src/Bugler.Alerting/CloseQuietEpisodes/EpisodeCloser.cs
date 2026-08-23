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

        // An Episode has no single Service any more (ADR 0034), so who is still being watched is
        // a question about its Participations — one query for the whole sweep.
        var openIds = openEpisodes.Select(e => e.Id).ToList();
        var participants = (await dbContext.Participations.AsNoTracking()
                .Where(p => openIds.Contains(p.EpisodeId))
                .Select(p => new { p.EpisodeId, p.ServiceId })
                .Distinct()
                .ToListAsync(cancellationToken))
            .GroupBy(p => p.EpisodeId)
            .ToDictionary(g => g.Key, g => g.Select(p => p.ServiceId).ToList());

        var mutedByWatch = new Dictionary<Watch, List<ServiceId>>();

        foreach (var episode in openEpisodes)
        {
            var fedBy = ServicesFeeding(episode, participants);
            var reason = CloseDecision.Decide(
                IsWatchOff(episode, effective, fedBy),
                episode.AcknowledgedAt is not null,
                episode.ClaimedByDelegationId is not null,
                episode.LastMatchAt,
                // Sensitivity and the Quiet Window stay per Service; the kind of trouble's own
                // override is the Scope's. An Episode fed by Services configured differently
                // resolves against the one that opened it — the fallback, not the override.
                effective.QuietWindowOf(
                    episode.OpenedByServiceId ?? fedBy.FirstOrDefault(),
                    episode.ScopeKey,
                    episode.Fingerprint),
                now);

            switch (reason)
            {
                case EpisodeCloseReason.WatchOff:
                    // SilentClose closes and lapses below, per Watch.
                    mutedByWatch.TryAdd(episode.Watch, []);
                    mutedByWatch[episode.Watch].AddRange(fedBy);
                    break;

                case EpisodeCloseReason.QuietWindow:
                    episode.ClosedAt = now;
                    episode.CloseReason = EpisodeCloseReason.QuietWindow;
                    logger.LogInformation(
                        "Episode in scope {ScopeKey} fell quiet after {Errors} errors "
                        + "and {Warns} warnings",
                        episode.ScopeKey, episode.ErrorCount, episode.WarnCount);
                    break;
            }
        }

        foreach (var (watch, services) in mutedByWatch)
        {
            await SilentClose.ApplyAsync(
                dbContext, services.Distinct().ToList(), watch, now, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Who is still feeding this Episode. Every Episode holds at least one Participation — both
    /// watches write one as they open, and the Deletion cascade takes an Episode down with its
    /// last one — so an empty answer here means the Episode is already gone in all but name.
    /// </summary>
    private static List<ServiceId> ServicesFeeding(
        Episode episode, IReadOnlyDictionary<Guid, List<ServiceId>> participants) =>
        participants.GetValueOrDefault(episode.Id, []);

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

    /// <summary>
    /// Each Watch has its own switch: Sensitivity for the Logs Watch, an address for the Health
    /// Check Watch. An Episode is Muted only when every Service still feeding it has had that
    /// switch turned off — one tenant going quiet must not end an Episode the others are still
    /// filling. An Episode nothing feeds any more (its Services Deleted) is off by definition.
    /// </summary>
    private static bool IsWatchOff(
        Episode episode, EffectiveSettings effective, IReadOnlyList<ServiceId> fedBy) =>
        fedBy.Count > 0 && episode.Watch switch
        {
            Watch.Logs => fedBy.All(id => effective.SensitivityOf(id) == Sensitivity.Off),
            Watch.HealthCheck => fedBy.All(id => effective.HealthCheckUrlOf(id) is null),
            _ => false,
        };
}
