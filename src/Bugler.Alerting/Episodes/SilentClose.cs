using Bugler.Alerting.Deliveries;
using Bugler.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Alerting.Episodes;

/// <summary>
/// Closes the open Episodes of Services whose Sensitivity turned Off: immediately, with no All
/// Clear (nothing was resolved — the watching stopped), and with their still-pending Deliveries
/// lapsed, because an Alert arriving after the mute is exactly the stale panic the TTL exists
/// to prevent. Tracked changes only — the caller's SaveChanges commits it atomically.
/// </summary>
internal static class SilentClose
{
    public static async Task ApplyAsync(
        AlertingDbContext dbContext,
        IReadOnlyCollection<ServiceId> serviceIds,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (serviceIds.Count == 0)
        {
            return;
        }

        var episodes = await dbContext.Episodes
            .Where(e => serviceIds.Contains(e.ServiceId) && e.ClosedAt == null)
            .ToListAsync(cancellationToken);
        if (episodes.Count == 0)
        {
            return;
        }

        foreach (var episode in episodes)
        {
            episode.ClosedAt = now;
            episode.CloseReason = EpisodeCloseReason.WatchOff;
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
