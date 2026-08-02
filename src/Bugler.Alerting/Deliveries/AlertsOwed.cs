using Bugler.Alerting.Episodes;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Alerting.Deliveries;

/// <summary>
/// The messages a freshly opened Episode owes: one mail per subscriber, one chat message if the
/// Application holds a webhook. Shared by every Watch on purpose — what an Episode announces must
/// not depend on which watch found it, or the two would drift apart one fix at a time.
/// Tracked changes only; the caller's transaction commits them with the Episode itself.
/// </summary>
internal static class AlertsOwed
{
    public static async Task EnqueueAsync(
        AlertingDbContext dbContext,
        Episode episode,
        bool mailEnabled,
        bool chatConfigured,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (mailEnabled)
        {
            // No access check here: whether a subscriber may still read the Application is a
            // delivery-time question. Subscribers are resolved at open — the Alert's audience.
            var subscribers = await dbContext.Subscriptions
                .Where(s => s.ServiceId == episode.ServiceId || s.ApplicationId == episode.ApplicationId)
                .Select(s => s.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);
            foreach (var userId in subscribers)
            {
                dbContext.Deliveries.Add(new Delivery
                {
                    Id = Guid.CreateVersion7(),
                    EpisodeId = episode.Id,
                    Kind = DeliveryKind.Alert,
                    Channel = DeliveryChannel.Mail,
                    UserId = userId,
                    CreatedAt = now,
                    NextAttemptAt = now,
                });
            }
        }

        if (chatConfigured)
        {
            dbContext.Deliveries.Add(new Delivery
            {
                Id = Guid.CreateVersion7(),
                EpisodeId = episode.Id,
                Kind = DeliveryKind.Alert,
                Channel = DeliveryChannel.Chat,
                CreatedAt = now,
                NextAttemptAt = now,
            });
        }
    }
}
