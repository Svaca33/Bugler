using Bugler.Alerting.Episodes;
using Bugler.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Alerting.Deliveries;

/// <summary>
/// The messages an Episode owes the people it concerns. Exactly one per Episode per recipient
/// (ADR 0034), never one per subscription and never one per channel: following both an
/// Application and one of its Services is told once, and a Service that falls into a running
/// Episode owes its own followers a message only if they have not already had one.
///
/// Shared by every Watch on purpose — what an Episode announces must not depend on which watch
/// found it, or the two would drift apart one fix at a time. Tracked changes only; the caller's
/// transaction commits them with the Episode itself.
/// </summary>
internal static class AlertsOwed
{
    /// <summary>
    /// What a freshly opened Episode owes: the Application's followers and the opening Service's,
    /// plus the Application's Chat Webhook where one is held.
    /// </summary>
    public static async Task EnqueueOpeningAsync(
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
            var subscribers = await FollowersAsync(
                dbContext, episode.ApplicationId, episode.OpenedByServiceId, cancellationToken);
            foreach (var userId in subscribers)
            {
                Add(dbContext, episode, DeliveryKind.Alert, DeliveryChannel.Mail, userId, now);
            }
        }

        if (chatConfigured)
        {
            Add(dbContext, episode, DeliveryKind.Alert, DeliveryChannel.Chat, null, now);
        }
    }

    /// <summary>
    /// What a Service falling into a running Episode owes (see CONTEXT.md: Alert): its own
    /// followers, and only those holding no message about this Episode yet — otherwise somebody
    /// following one tenant alone would never hear that their tenant is affected, which is
    /// precisely what a per-Service Subscription is for. The Chat Webhook is not told again: it
    /// already carries the Episode, and one space does not follow one tenant.
    /// </summary>
    public static async Task EnqueueJoiningAsync(
        AlertingDbContext dbContext,
        Episode episode,
        ServiceId joining,
        bool mailEnabled,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (!mailEnabled || episode.AlertFoldedIntoStorm)
        {
            return;
        }

        var followers = await dbContext.Subscriptions
            .Where(s => s.ServiceId == joining)
            .Select(s => s.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (followers.Count == 0)
        {
            return;
        }

        var alreadyTold = await AlreadyToldAsync(dbContext, episode.Id, cancellationToken);
        foreach (var userId in followers.Where(userId => !alreadyTold.Contains(userId)))
        {
            Add(dbContext, episode, DeliveryKind.Joined, DeliveryChannel.Mail, userId, now,
                joining: joining);
        }
    }

    /// <summary>
    /// The one message a Storm sends in place of the Alerts it folded (see CONTEXT.md: Storm) —
    /// one per Episode Scope per window, to everyone the Scope's Application concerns and to its
    /// Chat Webhook. <paramref name="foldedCount"/> is what it names.
    /// </summary>
    public static async Task EnqueueStormDigestAsync(
        AlertingDbContext dbContext,
        Episode episode,
        int foldedCount,
        bool mailEnabled,
        bool chatConfigured,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (mailEnabled)
        {
            var subscribers = await FollowersAsync(
                dbContext, episode.ApplicationId, episode.OpenedByServiceId, cancellationToken);
            foreach (var userId in subscribers)
            {
                Add(dbContext, episode, DeliveryKind.StormDigest, DeliveryChannel.Mail, userId,
                    now, foldedCount);
            }
        }

        if (chatConfigured)
        {
            Add(dbContext, episode, DeliveryKind.StormDigest, DeliveryChannel.Chat, null, now,
                foldedCount);
        }
    }

    private static Task<List<Guid>> FollowersAsync(
        AlertingDbContext dbContext,
        ApplicationId applicationId,
        ServiceId? serviceId,
        CancellationToken cancellationToken) =>
        dbContext.Subscriptions
            .Where(s => s.ApplicationId == applicationId
                || (serviceId != null && s.ServiceId == serviceId))
            .Select(s => s.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Who already holds an announcing message about this Episode — saved rows and the ones this
    /// very transaction has just added, because an opening and a joining can land in one page.
    /// </summary>
    private static async Task<HashSet<Guid>> AlreadyToldAsync(
        AlertingDbContext dbContext, Guid episodeId, CancellationToken cancellationToken)
    {
        var saved = await dbContext.Deliveries
            .Where(d => d.EpisodeId == episodeId
                && d.UserId != null
                && (d.Kind == DeliveryKind.Alert || d.Kind == DeliveryKind.Joined))
            .Select(d => d.UserId!.Value)
            .ToListAsync(cancellationToken);

        var pending = dbContext.ChangeTracker.Entries<Delivery>()
            .Where(entry => entry.State == EntityState.Added)
            .Select(entry => entry.Entity)
            .Where(d => d.EpisodeId == episodeId
                && d.UserId is not null
                && d.Kind is DeliveryKind.Alert or DeliveryKind.Joined)
            .Select(d => d.UserId!.Value);

        return saved.Concat(pending).ToHashSet();
    }

    private static void Add(
        AlertingDbContext dbContext,
        Episode episode,
        DeliveryKind kind,
        DeliveryChannel channel,
        Guid? userId,
        DateTimeOffset now,
        int? foldedCount = null,
        ServiceId? joining = null) =>
        dbContext.Deliveries.Add(new Delivery
        {
            Id = Guid.CreateVersion7(),
            EpisodeId = episode.Id,
            Kind = kind,
            Channel = channel,
            UserId = userId,
            CreatedAt = now,
            NextAttemptAt = now,
            FoldedEpisodeCount = foldedCount,
            JoiningServiceId = joining,
        });
}
