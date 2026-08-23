using System.Security.Claims;
using Bugler.Access.Contracts;
using Bugler.Alerting.DetectEpisodes;
using Bugler.Alerting.Episodes;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Alerting.ListEpisodes;

/// <summary>What one Service running one version put into an Episode (see CONTEXT.md: Participation).</summary>
public sealed record ParticipationDto(
    Guid ServiceId,
    string? Version,
    DateTimeOffset FirstAt,
    DateTimeOffset LastAt,
    int ErrorCount,
    int WarnCount);

public sealed record EpisodeDto(
    Guid Id,
    Guid ApplicationId,
    /// <summary>The Service whose Match opened it — evidence, not an owner; null once that Service is Deleted.</summary>
    Guid? OpenedByServiceId,
    /// <summary>How far this Episode reaches (see CONTEXT.md: Episode Scope) — the grouping key, opaque to the reader.</summary>
    string ScopeKey,
    Watch Watch,
    /// <summary>Opaque since ADR 0033: what tells kinds apart, never what a person reads.</summary>
    string Fingerprint,
    /// <summary>The readable name of the trouble (see CONTEXT.md: Title).</summary>
    string Title,
    /// <summary>Which rung of the ladder produced the Fingerprint — the visible degradation.</summary>
    FingerprintRung FingerprintRung,
    /// <summary>Which recipe distilled it; 0 is a row from before the recipe existed, and never re-read.</summary>
    int RecipeVersion,
    /// <summary>Whether the opening stack was too long to read whole, so the grouping may be coarser than it could.</summary>
    bool StackTruncated,
    /// <summary>Whether this Episode's Alerts were folded into a Storm digest (see CONTEXT.md: Storm).</summary>
    bool AlertFoldedIntoStorm,
    /// <summary>Which Services and versions are in it (see CONTEXT.md: Participation).</summary>
    IReadOnlyList<ParticipationDto> Participations,
    EpisodeState State,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt,
    DateTimeOffset LastMatchAt,
    int ErrorCount,
    int WarnCount,
    long? FirstMatchLogId,
    DateTimeOffset FirstMatchAt,
    short? FirstMatchSeverity,
    string? FirstMatchDetail,
    DateTimeOffset? AcknowledgedAt,
    string? AcknowledgedBy,
    DateTimeOffset? SolvedAt,
    string? SolvedBy,
    /// <summary>The newest acknowledgement still held by an earlier Episode of this kind — the "somebody
    /// is on it" context a fresh Episode shows. Solve wipes the kind's acknowledgements (ADR 0005), so
    /// whatever this names is by definition unresolved work.</summary>
    DateTimeOffset? EarlierAcknowledgedAt,
    string? EarlierAcknowledgedBy,
    int PriorCount,
    /// <summary>The Quiet Window this kind of trouble keeps for itself; null means it inherits the Service's.</summary>
    int? FingerprintQuietWindowMinutes,
    /// <summary>The machine hand's live marks, where one stands (see CONTEXT.md: Machine Claim).</summary>
    MachineClaimDto? MachineClaim,
    MachineNoteDto? MachineNote,
    SolvedProposalDto? SolvedProposal,
    ResignationDto? Resignation);

public sealed record ListEpisodesResponse(IReadOnlyList<EpisodeDto> Items);

internal static class EpisodesEndpoint
{
    /// <summary>
    /// Episodes within the caller's Visibility Scope, newest first. Scope needs no catalog
    /// round-trip here: an Episode carries its ApplicationId and grants are per Application.
    /// PriorCount is how many Episodes of the same kind of trouble came before this one — the
    /// recurrence the UI groups on.
    /// </summary>
    public static async Task<IResult> Handle(
        Guid? applicationId,
        Guid[]? serviceId,
        EpisodeState[]? state,
        string? scopeKey,
        string? fingerprint,
        DateTimeOffset? from,
        string? q,
        string? acknowledged,
        bool? latestPerFingerprint,
        Guid? beforeId,
        int? limit,
        ClaimsPrincipal principal,
        AlertingDbContext dbContext,
        IReadVisibility readVisibility,
        IUserNames userNames,
        IMachineDelegationNames delegationNames,
        CancellationToken cancellationToken)
    {
        if (GetUserId(principal) is not { } callerId)
        {
            return Results.Unauthorized();
        }

        if (!EpisodeFilter.IsValidAcknowledged(acknowledged))
        {
            return Results.BadRequest("acknowledged must be \"none\" or \"me\".");
        }

        var visible = await readVisibility.GetVisibleApplicationsAsync(cancellationToken);
        if (visible is { Count: 0 })
        {
            return Results.Ok(new ListEpisodesResponse([]));
        }

        var query = dbContext.Episodes.AsNoTracking()
            .Apply(dbContext, visible, applicationId, serviceId, scopeKey, fingerprint, from, q,
                acknowledged, callerId);

        if (latestPerFingerprint == true)
        {
            // The grouped list: one row per kind of trouble, faced by its latest Episode. Every
            // filter above and below then judges the face — a group is shown or hidden whole.
            query = query.WhereLatestPerFingerprint(dbContext.Episodes);
        }

        if (state is { Length: > 0 })
        {
            // The state is derived, never stored (ADR 0003), so each wanted one becomes its
            // defining predicate; the flags constant-fold out of the SQL.
            var wantOpen = state.Contains(EpisodeState.Open);
            var wantQuieted = state.Contains(EpisodeState.Quieted);
            var wantSolved = state.Contains(EpisodeState.Solved);
            var wantMuted = state.Contains(EpisodeState.Muted);
            query = query.Where(e =>
                (wantOpen && e.ClosedAt == null)
                || (wantQuieted && e.SolvedAt == null
                    && e.CloseReason == EpisodeCloseReason.QuietWindow)
                || (wantSolved && e.SolvedAt != null)
                || (wantMuted && e.SolvedAt == null && e.ClosedAt != null
                    && e.CloseReason != EpisodeCloseReason.QuietWindow));
        }

        // Episode ids are UUIDv7 and PostgreSQL compares uuids bytewise, so id order is open
        // order — one keyset column covers the (opened_at, id) ordering.
        if (beforeId is { } before)
        {
            query = query.Where(e => e.Id.CompareTo(before) < 0);
        }

        var take = Math.Clamp(limit ?? 100, 1, 500);
        var rows = await query
            .OrderByDescending(e => e.Id)
            .Take(take)
            .Select(e => new
            {
                Episode = e,
                PriorCount = dbContext.Episodes.Count(p =>
                    p.ScopeKey == e.ScopeKey
                    && p.Fingerprint == e.Fingerprint
                    && p.Id.CompareTo(e.Id) < 0),
                // Actions land only on the newest Episode of a kind, so the newest earlier
                // Episode holding an acknowledgement also holds the kind's newest one.
                EarlierAck = dbContext.Episodes
                    .Where(p => p.ScopeKey == e.ScopeKey
                        && p.Fingerprint == e.Fingerprint
                        && p.Id.CompareTo(e.Id) < 0
                        && p.AcknowledgedByUserId != null)
                    .OrderByDescending(p => p.Id)
                    .Select(p => new { p.AcknowledgedByUserId, p.AcknowledgedAt })
                    .FirstOrDefault(),
                // Keyed on the new table's primary key, so this is a lookup, not a scan.
                FingerprintQuietWindowMinutes = dbContext.FingerprintQuietWindows
                    .Where(w => w.ScopeKey == e.ScopeKey && w.Fingerprint == e.Fingerprint)
                    .Select(w => (int?)w.QuietWindowMinutes)
                    .FirstOrDefault(),
                // Only a proposal or a Resignation ages into "overtaken", so the sibling check
                // is paid only where one stands.
                NewerExists = (e.ProposedAt != null || e.ResignedAt != null)
                    && dbContext.Episodes.Any(p =>
                        p.ScopeKey == e.ScopeKey
                        && p.Fingerprint == e.Fingerprint
                        && p.Id.CompareTo(e.Id) > 0),
            })
            .ToListAsync(cancellationToken);

        // One query for the whole page: which Services and versions are in each row's Episode —
        // the "does the new version still do it" read the list leads with.
        var participations = await ParticipationsOfAsync(
            dbContext, rows.Select(r => r.Episode.Id).ToList(), cancellationToken);

        var names = await userNames.ResolveAsync(
            rows.SelectMany(r => new[]
                {
                    r.Episode.AcknowledgedByUserId, r.Episode.SolvedByUserId,
                    r.EarlierAck?.AcknowledgedByUserId,
                })
                .OfType<Guid>()
                .ToHashSet(),
            cancellationToken);
        var machines = await delegationNames.ResolveAsync(
            rows.SelectMany(r => MachineHandDtos.DelegationIds(r.Episode)).ToHashSet(),
            cancellationToken);

        var items = rows.Select(r => new EpisodeDto(
            r.Episode.Id, r.Episode.ApplicationId.Value, r.Episode.OpenedByServiceId?.Value,
            r.Episode.ScopeKey, r.Episode.Watch, r.Episode.Fingerprint, r.Episode.Title,
            r.Episode.FingerprintRung, r.Episode.RecipeVersion, r.Episode.StackTruncated,
            r.Episode.AlertFoldedIntoStorm,
            participations.GetValueOrDefault(r.Episode.Id, []),
            r.Episode.State, r.Episode.OpenedAt,
            r.Episode.ClosedAt, r.Episode.LastMatchAt, r.Episode.ErrorCount, r.Episode.WarnCount,
            r.Episode.FirstMatchLogId, r.Episode.FirstMatchAt, r.Episode.FirstMatchSeverity,
            r.Episode.FirstMatchDetail,
            r.Episode.AcknowledgedAt, NameOf(names, r.Episode.AcknowledgedByUserId),
            r.Episode.SolvedAt, NameOf(names, r.Episode.SolvedByUserId),
            r.EarlierAck?.AcknowledgedAt, NameOf(names, r.EarlierAck?.AcknowledgedByUserId),
            r.PriorCount, r.FingerprintQuietWindowMinutes,
            MachineHandDtos.Claim(r.Episode, machines),
            MachineHandDtos.Note(r.Episode, machines),
            MachineHandDtos.Proposal(r.Episode, r.NewerExists, machines),
            MachineHandDtos.Resignation(r.Episode, r.NewerExists, machines))).ToList();

        return Results.Ok(new ListEpisodesResponse(items));
    }

    /// <summary>Which Services and versions are in each of these Episodes, oldest sighting first.</summary>
    internal static async Task<Dictionary<Guid, IReadOnlyList<ParticipationDto>>> ParticipationsOfAsync(
        AlertingDbContext dbContext,
        IReadOnlyList<Guid> episodeIds,
        CancellationToken cancellationToken)
    {
        if (episodeIds.Count == 0)
        {
            return [];
        }

        var rows = await dbContext.Participations.AsNoTracking()
            .Where(p => episodeIds.Contains(p.EpisodeId))
            .OrderBy(p => p.FirstAt)
            .Select(p => new
            {
                p.EpisodeId,
                Dto = new ParticipationDto(
                    p.ServiceId.Value, p.Version, p.FirstAt, p.LastAt, p.ErrorCount, p.WarnCount),
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.EpisodeId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ParticipationDto>)group.Select(row => row.Dto).ToList());
    }

    /// <summary>Null when nobody holds the mark; a deleted User leaves the timestamp with no name.</summary>
    internal static string? NameOf(IReadOnlyDictionary<Guid, string> names, Guid? userId) =>
        userId is { } id && names.TryGetValue(id, out var name) ? name : null;

    // Access's claim helper is internal to Access; the two lines are cheaper than a contract.
    private static Guid? GetUserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
