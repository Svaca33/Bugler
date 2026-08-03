using Bugler.Access.Contracts;
using Bugler.Alerting.Deliveries;
using Bugler.Alerting.Episodes;
using Bugler.Mail;
using Bugler.Registry.Contracts;
using Bugler.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bugler.Alerting.DeliverMessages;

/// <summary>
/// One delivery sweep: lapse what outlived its time-to-live, then pursue what is due. Access,
/// addresses, service names and the webhook are all resolved now, at send time — never earlier.
/// Every attempt saves its own outcome: sends are network I/O outside any transaction, so a
/// crash between an accepted send and the save can duplicate one message (at-least-once).
/// </summary>
public sealed class DeliveryRunner(
    IServiceScopeFactory scopeFactory,
    IMailSender mailSender,
    IOptions<AlertingOptions> options,
    ILogger<DeliveryRunner> logger)
{
    private const int BatchSize = 100;

    public async Task DeliverOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AlertingDbContext>();
        var now = DateTimeOffset.UtcNow;

        await LapseExpiredAsync(dbContext, now, cancellationToken);

        // Alerts are the only messages Bugler sends (ADR 0003). Historical AllClear rows are
        // lapsed or delivered; the filter is belt and braces against composing one anyway.
        var due = await dbContext.Deliveries
            .Where(d => d.Kind == DeliveryKind.Alert
                && d.DeliveredAt == null && d.LapsedAt == null && d.NextAttemptAt <= now)
            .OrderBy(d => d.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);
        if (due.Count == 0)
        {
            return;
        }

        var episodeIds = due.Select(d => d.EpisodeId).Distinct().ToList();
        var episodes = await dbContext.Episodes
            .AsNoTracking()
            .Where(e => episodeIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, cancellationToken);

        var catalog = await scope.ServiceProvider.GetRequiredService<ICatalogReader>()
            .GetServicesAsync(cancellationToken);
        var identityByService = catalog.ToDictionary(s => s.Id);

        var recipients = await ResolveRecipientsAsync(
            scope.ServiceProvider.GetRequiredService<IMailRecipients>(), due, episodes,
            cancellationToken);
        var webhookByApplication = await LoadWebhooksAsync(dbContext, episodes, cancellationToken);
        var chatSender = scope.ServiceProvider.GetRequiredService<IChatSender>();

        // A chat card has no person behind it, so it is spoken in the server's language; each
        // mail is spoken in its recipient's, which rode in on the MailRecipient (ADR 0024).
        var chatLanguage = await scope.ServiceProvider.GetRequiredService<IServerLanguage>()
            .GetAsync(cancellationToken);

        foreach (var delivery in due)
        {
            var episode = episodes[delivery.EpisodeId];

            if (!identityByService.TryGetValue(episode.ServiceId, out var identity))
            {
                // The Service is gone; the cascade will collect this Episode shortly.
                Lapse(delivery, "The service is no longer registered.");
            }
            else if (delivery.Channel == DeliveryChannel.Mail)
            {
                await AttemptMailAsync(delivery, episode, identity, recipients, cancellationToken);
            }
            else
            {
                await AttemptChatAsync(
                    delivery, episode, identity, webhookByApplication, chatSender, chatLanguage,
                    cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task AttemptMailAsync(
        Delivery delivery,
        Episode episode,
        CatalogService identity,
        RecipientDirectory recipients,
        CancellationToken cancellationToken)
    {
        if (recipients.Unknown.Contains(delivery.UserId!.Value))
        {
            // Deleted user; the UserDeleted handler owns the cleanup, this row just ends.
            Lapse(delivery, "The user no longer exists.");
            return;
        }

        if (!recipients.ByUser.TryGetValue(delivery.UserId!.Value, out var recipient))
        {
            // Deactivated or ungranted right now. Dormant, not dead: they may be let back in
            // before the TTL runs out, so the row simply stays due.
            return;
        }

        var alert = MessageComposer.ComposeAlert(
            episode, identity, options.Value.PublicBaseUrl, recipient.Language);
        await AttemptAsync(
            delivery,
            () => mailSender.SendAsync(
                new MailMessage(recipient.Email, alert.Subject, alert.TextBody, alert.HtmlBody),
                cancellationToken));
    }

    private async Task AttemptChatAsync(
        Delivery delivery,
        Episode episode,
        CatalogService identity,
        IReadOnlyDictionary<ApplicationId, string> webhookByApplication,
        IChatSender chatSender,
        Language chatLanguage,
        CancellationToken cancellationToken)
    {
        if (!webhookByApplication.TryGetValue(episode.ApplicationId, out var webhookUrl))
        {
            // An admin who removes or rotates the webhook means it — the destination is gone.
            Lapse(delivery, "The application no longer has a chat webhook.");
            return;
        }

        var alert = MessageComposer.ComposeAlert(
            episode, identity, options.Value.PublicBaseUrl, chatLanguage);
        await AttemptAsync(
            delivery,
            () => chatSender.SendAsync(webhookUrl, alert, cancellationToken));
    }

    private async Task AttemptAsync(Delivery delivery, Func<Task> send)
    {
        try
        {
            await send();
            delivery.DeliveredAt = DateTimeOffset.UtcNow;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            delivery.Attempts += 1;
            delivery.LastError = Truncate(exception.Message);
            delivery.NextAttemptAt = DateTimeOffset.UtcNow + RetrySchedule.NextDelay(delivery.Attempts);
            logger.LogWarning(
                exception,
                "Delivery {DeliveryId} ({Kind} via {Channel}) failed on attempt {Attempts}",
                delivery.Id, delivery.Kind, delivery.Channel, delivery.Attempts);
        }
    }

    private async Task LapseExpiredAsync(
        AlertingDbContext dbContext, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var expiryLimit = now - TimeSpan.FromHours(options.Value.DeliveryTimeToLiveHours);
        var lapsed = await dbContext.Deliveries
            .Where(d => d.DeliveredAt == null && d.LapsedAt == null && d.CreatedAt < expiryLimit)
            .ExecuteUpdateAsync(
                d => d.SetProperty(x => x.LapsedAt, now), cancellationToken);
        if (lapsed > 0)
        {
            // A message this stale wakes more panic than none — logged here, into Bugler itself.
            logger.LogWarning("{Count} deliveries outlived their time-to-live and lapsed", lapsed);
        }
    }

    /// <summary>One Access ask per Application: which of these subscribers may be told right now?</summary>
    private static async Task<RecipientDirectory> ResolveRecipientsAsync(
        IMailRecipients mailRecipients,
        IReadOnlyList<Delivery> due,
        IReadOnlyDictionary<Guid, Episode> episodes,
        CancellationToken cancellationToken)
    {
        var byUser = new Dictionary<Guid, MailRecipient>();
        var unknown = new HashSet<Guid>();

        var byApplication = due
            .Where(d => d.UserId is not null)
            .GroupBy(d => episodes[d.EpisodeId].ApplicationId);
        foreach (var group in byApplication)
        {
            var userIds = group.Select(d => d.UserId!.Value).Distinct().ToList();
            var result = await mailRecipients.ResolveAsync(userIds, group.Key, cancellationToken);
            foreach (var recipient in result.Deliverable)
            {
                byUser[recipient.UserId] = recipient;
            }

            unknown.UnionWith(result.UnknownUserIds);
        }

        return new RecipientDirectory(byUser, unknown);
    }

    private static async Task<Dictionary<ApplicationId, string>> LoadWebhooksAsync(
        AlertingDbContext dbContext,
        IReadOnlyDictionary<Guid, Episode> episodes,
        CancellationToken cancellationToken)
    {
        var applicationIds = episodes.Values.Select(e => e.ApplicationId).Distinct().ToList();
        return await dbContext.ApplicationSettings
            .AsNoTracking()
            .Where(s => applicationIds.Contains(s.ApplicationId) && s.ChatWebhookUrl != null)
            .ToDictionaryAsync(s => s.ApplicationId, s => s.ChatWebhookUrl!, cancellationToken);
    }

    private static void Lapse(Delivery delivery, string reason)
    {
        delivery.LapsedAt = DateTimeOffset.UtcNow;
        delivery.LastError = reason;
    }

    private static string Truncate(string error) =>
        error.Length <= 2000 ? error : error[..2000];

    private sealed record RecipientDirectory(
        Dictionary<Guid, MailRecipient> ByUser, HashSet<Guid> Unknown);
}
