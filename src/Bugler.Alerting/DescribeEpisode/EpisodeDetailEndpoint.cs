using System.Text.Json.Serialization;
using Bugler.Access.Contracts;
using Bugler.Alerting.Deliveries;
using Bugler.Alerting.Episodes;
using Bugler.Alerting.ListEpisodes;
using Bugler.Alerting.Settings;
using Bugler.Registry.Contracts;
using Bugler.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Alerting.DescribeEpisode;

/// <summary>The mailed Alert: how many subscribed Users it went to, and when the first copy landed (null while none has).</summary>
public sealed record MailAlertDto(int SubscriberCount, DateTimeOffset? FirstDeliveredAt);

/// <summary>The Alert posted to the Application's Chat Webhook; DeliveredAt null while it has not landed.</summary>
public sealed record ChatAlertDto(DateTimeOffset? DeliveredAt);

/// <summary>
/// One Journal line (see CONTEXT.md: Journal); By is null when the User is no longer here.
/// Machine names the delegation where the hand was a machine's — and on the entries a person
/// writes over a machine's mark, the delegation whose mark it was.
/// </summary>
public sealed record JournalEntryDto(
    JournalEntryKind Kind, DateTimeOffset At, string? By, MachineHandByDto? Machine);

/// <summary>
/// The Episode's Reading (see CONTEXT.md: Reading) in every language Bugler speaks — the viewer's
/// own is the client's pick. Pending carries no text yet; Failed never will, and says nothing
/// beyond that the machine gave up: the evidence stands on its own.
/// </summary>
public sealed record ReadingDto(
    ReadingStateDto State,
    string? TextEn,
    string? TextCs,
    string? Model,
    DateTimeOffset? WrittenAt);

[JsonConverter(typeof(JsonStringEnumConverter<ReadingStateDto>))]
public enum ReadingStateDto
{
    Pending,
    Written,
    Failed,
}

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
    int InheritedQuietWindowMinutes,
    /// <summary>Every human hand laid on this Episode, oldest first (see CONTEXT.md: Journal).</summary>
    IReadOnlyList<JournalEntryDto> Journal,
    /// <summary>Null where none was ever owed — AI off or the Application unconsenting at the opening.</summary>
    ReadingDto? Reading);

internal static class EpisodeDetailEndpoint
{
    /// <summary>Outside the caller's Visibility Scope it 404s: not yours to see, not yours to know about.</summary>
    public static async Task<IResult> Handle(
        Guid id,
        AlertingDbContext dbContext,
        IReadVisibility readVisibility,
        IUserNames userNames,
        IMachineDelegationNames delegationNames,
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
            p.ScopeKey == episode.ScopeKey
            && p.Watch == episode.Watch
            && p.Fingerprint == episode.Fingerprint
            && p.Id.CompareTo(episode.Id) < 0, cancellationToken);

        // Actions land only on the newest Episode of a kind, so the newest earlier Episode
        // holding an acknowledgement also holds the kind's newest one.
        var earlierAck = await dbContext.Episodes.AsNoTracking()
            .Where(p => p.ScopeKey == episode.ScopeKey
                && p.Watch == episode.Watch
                && p.Fingerprint == episode.Fingerprint
                && p.Id.CompareTo(episode.Id) < 0
                && p.AcknowledgedByUserId != null)
            .OrderByDescending(p => p.Id)
            .Select(p => new { p.AcknowledgedByUserId, p.AcknowledgedAt })
            .FirstOrDefaultAsync(cancellationToken);

        // Oldest first: the Journal is read as the story ran. Same-instant entries (a Solve and
        // the withdrawals it wrote) keep insertion order via the identity key.
        var journal = await dbContext.JournalEntries.AsNoTracking()
            .Where(j => j.EpisodeId == id)
            .OrderBy(j => j.At).ThenBy(j => j.Id)
            .ToListAsync(cancellationToken);

        var alerts = await dbContext.Deliveries.AsNoTracking()
            .Where(d => d.EpisodeId == id
                && (d.Kind == DeliveryKind.Alert || d.Kind == DeliveryKind.Joined))
            .ToListAsync(cancellationToken);
        var reading = await dbContext.Readings.AsNoTracking()
            .FirstOrDefaultAsync(r => r.EpisodeId == id, cancellationToken);
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
            w => w.ScopeKey == episode.ScopeKey
                && w.Watch == episode.Watch
                && w.Fingerprint == episode.Fingerprint);
        var participations = await EpisodesEndpoint.ParticipationsOfAsync(
            dbContext, [episode.Id], cancellationToken);
        // Sensitivity and the Quiet Window's fallback stay per Service; the panel names them for
        // the Service that opened the Episode, or for whoever is still in it once that one is gone.
        var speakingFor = episode.OpenedByServiceId
            ?? participations.GetValueOrDefault(episode.Id, [])
                .Select(p => (ServiceId?)new ServiceId(p.ServiceId))
                .FirstOrDefault()
            ?? default;

        var names = await userNames.ResolveAsync(
            new[]
                {
                    episode.AcknowledgedByUserId, episode.SolvedByUserId, episode.ArchivedByUserId,
                    earlierAck?.AcknowledgedByUserId,
                }
                .OfType<Guid>()
                .Concat(journal.Select(j => j.UserId))
                .ToHashSet(),
            cancellationToken);
        var machines = await delegationNames.ResolveAsync(
            MachineHandDtos.DelegationIds(episode)
                .Concat(journal.Select(j => j.DelegationId).OfType<Guid>())
                .ToHashSet(),
            cancellationToken);

        // Only a proposal or a Resignation ages into "overtaken", so the check is paid only then.
        var newerExists = (episode.ProposedAt is not null || episode.ResignedAt is not null)
            && await dbContext.Episodes.AnyAsync(p =>
                p.ScopeKey == episode.ScopeKey
                && p.Watch == episode.Watch
                && p.Fingerprint == episode.Fingerprint
                && p.Id.CompareTo(episode.Id) > 0, cancellationToken);

        var dto = new EpisodeDto(
            episode.Id, episode.ApplicationId.Value, episode.OpenedByServiceId?.Value,
            episode.ScopeKey, episode.Watch, episode.Fingerprint, episode.Title,
            episode.FingerprintRung, episode.RecipeVersion, episode.StackTruncated,
            episode.AlertFoldedIntoStorm,
            participations.GetValueOrDefault(episode.Id, []),
            episode.State, episode.OpenedAt, episode.ClosedAt,
            episode.LastMatchAt, episode.ErrorCount, episode.WarnCount,
            episode.FirstMatchLogId, episode.FirstMatchAt, episode.FirstMatchSeverity,
            episode.FirstMatchDetail,
            episode.AcknowledgedAt, EpisodesEndpoint.NameOf(names, episode.AcknowledgedByUserId),
            episode.SolvedAt, EpisodesEndpoint.NameOf(names, episode.SolvedByUserId),
            episode.ArchivedAt, EpisodesEndpoint.NameOf(names, episode.ArchivedByUserId),
            earlierAck?.AcknowledgedAt,
            EpisodesEndpoint.NameOf(names, earlierAck?.AcknowledgedByUserId),
            priorCount, own?.QuietWindowMinutes,
            MachineHandDtos.Claim(episode, machines),
            MachineHandDtos.Note(episode, machines),
            MachineHandDtos.Proposal(episode, newerExists, machines),
            MachineHandDtos.Resignation(episode, newerExists, machines));

        return Results.Ok(new EpisodeDetailDto(
            dto,
            mail.Count > 0
                ? new MailAlertDto(
                    mail.Count,
                    mail.Where(d => d.DeliveredAt is not null).Min(d => d.DeliveredAt))
                : null,
            chat is null ? null : new ChatAlertDto(chat.DeliveredAt),
            effective.SensitivityOf(speakingFor),
            effective.QuietWindowMinutesOf(
                speakingFor, episode.ScopeKey, episode.Watch, episode.Fingerprint),
            effective.InheritedQuietWindowMinutesOf(speakingFor),
            journal.Select(j => new JournalEntryDto(
                j.Kind, j.At, EpisodesEndpoint.NameOf(names, j.UserId),
                j.DelegationId is { } machine
                    ? machines.TryGetValue(machine, out var name)
                        ? new MachineHandByDto(name.Name, name.HolderEmail)
                        : new MachineHandByDto(null, null)
                    : null)).ToList(),
            reading is null
                ? null
                : new ReadingDto(
                    reading.WrittenAt is not null ? ReadingStateDto.Written
                    : reading.FailedAt is not null ? ReadingStateDto.Failed
                    : ReadingStateDto.Pending,
                    reading.English, reading.Czech, reading.Model, reading.WrittenAt)));
    }
}
