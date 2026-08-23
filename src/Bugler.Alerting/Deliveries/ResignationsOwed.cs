using Bugler.Alerting.Episodes;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Alerting.Deliveries;

/// <summary>
/// The messages a freshly laid Resignation owes: one mail per subscriber, one chat message if
/// the Application holds a webhook — the Alert's audience, resolved the Alert's way, because a
/// Resignation is the machine calling for exactly the people the trouble already concerns. A
/// Solved Proposal owes nothing (its PR notifies the code side); a Resignation has no PR, so if
/// nobody is told, nobody comes. Tracked changes only; the caller's transaction commits them
/// with the mark itself.
/// </summary>
internal static class ResignationsOwed
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
            // Everyone the Episode concerns: the Application's followers and the followers of
            // every Service in it — a Resignation calls for a human hand, and whose hand it is
            // depends on which tenant is affected.
            var subscribers = await dbContext.Subscriptions
                .Where(s => s.ApplicationId == episode.ApplicationId
                    || (s.ServiceId != null && dbContext.Participations.Any(p =>
                        p.EpisodeId == episode.Id && p.ServiceId == s.ServiceId!.Value)))
                .Select(s => s.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);
            foreach (var userId in subscribers)
            {
                dbContext.Deliveries.Add(new Delivery
                {
                    Id = Guid.CreateVersion7(),
                    EpisodeId = episode.Id,
                    Kind = DeliveryKind.Resignation,
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
                Kind = DeliveryKind.Resignation,
                Channel = DeliveryChannel.Chat,
                CreatedAt = now,
                NextAttemptAt = now,
            });
        }
    }
}
