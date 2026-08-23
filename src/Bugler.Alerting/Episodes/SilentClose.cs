using Bugler.Alerting.Deliveries;
using Bugler.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Alerting.Episodes;

/// <summary>
/// Closes the open Episodes one Watch was feeding in Services where that Watch has been turned
/// off: immediately, with no All Clear (nothing was resolved — the watching stopped), and with
/// their still-pending Deliveries lapsed, because an Alert arriving after the mute is exactly the
/// stale panic the TTL exists to prevent. One Watch at a time on purpose: turning Sensitivity Off
/// must not close an Episode the Health Check Watch is still feeding, nor the other way round.
/// Tracked changes only — the caller's SaveChanges commits it atomically.
/// </summary>
internal static class SilentClose
{
    public static async Task ApplyAsync(
        AlertingDbContext dbContext,
        IReadOnlyCollection<ServiceId> serviceIds,
        Watch watch,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (serviceIds.Count == 0)
        {
            return;
        }

        // An Episode has no single Service (ADR 0034): it belongs to everyone feeding it, so
        // turning one tenant's watch off must not end an Episode the others are still filling.
        // Only an Episode whose every participant has gone silent has had its watching stop.
        var ids = serviceIds.ToList();
        var episodes = await dbContext.Episodes
            .Where(e => e.Watch == watch && e.ClosedAt == null
                && dbContext.Participations.Any(p =>
                    p.EpisodeId == e.Id && ids.Contains(p.ServiceId))
                && !dbContext.Participations.Any(p =>
                    p.EpisodeId == e.Id && !ids.Contains(p.ServiceId)))
            .ToListAsync(cancellationToken);
        if (episodes.Count == 0)
        {
            return;
        }

        foreach (var episode in episodes)
        {
            episode.ClosedAt = now;
            episode.CloseReason = EpisodeCloseReason.WatchOff;

            // A claim on an Episode the watching left behind holds nothing; it falls off with
            // the close, and the Journal says so rather than letting the mark wilt in silence.
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

        var episodeIds = episodes.Select(e => e.Id).ToList();
        var pending = await dbContext.Deliveries
            .Where(d => episodeIds.Contains(d.EpisodeId) && d.DeliveredAt == null && d.LapsedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var delivery in pending)
        {
            delivery.LapsedAt = now;
        }
    }
}
