using Bugler.Access.Contracts;
using Bugler.Alerting.Deliveries;
using Bugler.Alerting.ListEpisodes;
using Bugler.Alerting.Settings;
using Bugler.Registry.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Alerting.DescribeEpisode;

/// <summary>The mailed Alert: how many subscribed Users it went to, and when the first copy landed (null while none has).</summary>
public sealed record MailAlertDto(int SubscriberCount, DateTimeOffset? FirstDeliveredAt);

/// <summary>The Alert posted to the Application's Chat Webhook; DeliveredAt null while it has not landed.</summary>
public sealed record ChatAlertDto(DateTimeOffset? DeliveredAt);

/// <summary>
/// One Episode with what the timeline needs beyond the list row. The settings are the effective
/// values as configured now — detection always reads the current value, so for an open Episode
/// they are exactly what will close it.
/// </summary>
public sealed record EpisodeDetailDto(
    EpisodeDto Episode,
    MailAlertDto? MailAlert,
    ChatAlertDto? ChatAlert,
    Sensitivity EffectiveSensitivity,
    /// <summary>What actually closes this Episode: its kind's own window where set, the Service's otherwise.</summary>
    int QuietWindowMinutes,
    /// <summary>What this kind would fall back to — so the panel can name the inheritance without guessing.</summary>
    int InheritedQuietWindowMinutes);

internal static class EpisodeDetailEndpoint
{
    /// <summary>Outside the caller's Visibility Scope it 404s: not yours to see, not yours to know about.</summary>
    public static async Task<IResult> Handle(
        Guid id,
        AlertingDbContext dbContext,
        IReadVisibility readVisibility,
        IUserNames userNames,
        ICatalogReader catalogReader,
        CancellationToken cancellationToken)
    {
        var episode = await dbContext.Episodes.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (episode is null)
        {
            return Results.NotFound();
        }

        var visible = await readVisibility.GetVisibleApplicationsAsync(cancellationToken);
        if (visible is not null && !visible.Contains(episode.ApplicationId))
        {
            return Results.NotFound();
        }

        var priorCount = await dbContext.Episodes.CountAsync(p =>
            p.ServiceId == episode.ServiceId
            && p.Fingerprint == episode.Fingerprint
            && p.Id.CompareTo(episode.Id) < 0, cancellationToken);

        // Actions land only on the newest Episode of a kind, so the newest earlier Episode
        // holding an acknowledgement also holds the kind's newest one.
        var earlierAck = await dbContext.Episodes.AsNoTracking()
            .Where(p => p.ServiceId == episode.ServiceId
                && p.Fingerprint == episode.Fingerprint
                && p.Id.CompareTo(episode.Id) < 0
                && p.AcknowledgedByUserId != null)
            .OrderByDescending(p => p.Id)
            .Select(p => new { p.AcknowledgedByUserId, p.AcknowledgedAt })
            .FirstOrDefaultAsync(cancellationToken);

        var alerts = await dbContext.Deliveries.AsNoTracking()
            .Where(d => d.EpisodeId == id && d.Kind == DeliveryKind.Alert)
            .ToListAsync(cancellationToken);
        var mail = alerts.Where(d => d.Channel == DeliveryChannel.Mail).ToList();
        var chat = alerts.FirstOrDefault(d => d.Channel == DeliveryChannel.Chat);

        // Effective settings resolve through the whole catalog (service ?? application ?? default),
        // the same way detection builds them — one shared path, no second opinion.
        var catalog = await catalogReader.GetServicesAsync(cancellationToken);
        var fingerprintWindows = await dbContext.FingerprintQuietWindows.AsNoTracking()
            .ToListAsync(cancellationToken);
        var effective = EffectiveSettings.Build(
            catalog,
            await dbContext.ApplicationSettings.AsNoTracking().ToListAsync(cancellationToken),
            await dbContext.ServiceSettings.AsNoTracking().ToListAsync(cancellationToken),
            fingerprintWindows);
        var own = fingerprintWindows.FirstOrDefault(
            w => w.ServiceId == episode.ServiceId && w.Fingerprint == episode.Fingerprint);

        var names = await userNames.ResolveAsync(
            new[] { episode.AcknowledgedByUserId, episode.SolvedByUserId, earlierAck?.AcknowledgedByUserId }
                .OfType<Guid>().ToHashSet(),
            cancellationToken);

        var dto = new EpisodeDto(
            episode.Id, episode.ApplicationId.Value, episode.ServiceId.Value,
            episode.Fingerprint, episode.State, episode.OpenedAt, episode.ClosedAt,
            episode.LastMatchAt, episode.ErrorCount, episode.WarnCount,
            episode.FirstLogId, episode.FirstLogTimestamp, episode.FirstLogSeverity,
            episode.FirstLogBody,
            episode.AcknowledgedAt, EpisodesEndpoint.NameOf(names, episode.AcknowledgedByUserId),
            episode.SolvedAt, EpisodesEndpoint.NameOf(names, episode.SolvedByUserId),
            earlierAck?.AcknowledgedAt,
            EpisodesEndpoint.NameOf(names, earlierAck?.AcknowledgedByUserId),
            priorCount, own?.QuietWindowMinutes);

        return Results.Ok(new EpisodeDetailDto(
            dto,
            mail.Count > 0
                ? new MailAlertDto(
                    mail.Count,
                    mail.Where(d => d.DeliveredAt is not null).Min(d => d.DeliveredAt))
                : null,
            chat is null ? null : new ChatAlertDto(chat.DeliveredAt),
            effective.SensitivityOf(episode.ServiceId),
            effective.QuietWindowMinutesOf(episode.ServiceId, episode.Fingerprint),
            effective.InheritedQuietWindowMinutesOf(episode.ServiceId)));
    }
}
