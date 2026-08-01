using System.Security.Claims;
using Bugler.Access.Contracts;
using Bugler.Alerting.Episodes;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Alerting.ListEpisodes;

public sealed record EpisodeDto(
    Guid Id,
    Guid ApplicationId,
    Guid ServiceId,
    string Fingerprint,
    EpisodeState State,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt,
    DateTimeOffset LastMatchAt,
    int ErrorCount,
    int WarnCount,
    long FirstLogId,
    DateTimeOffset FirstLogTimestamp,
    short FirstLogSeverity,
    string? FirstLogBody,
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
    int? FingerprintQuietWindowMinutes);

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
            .Apply(visible, applicationId, serviceId, fingerprint, from, q, acknowledged, callerId);

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
                || (wantMuted && e.SolvedAt == null
                    && e.CloseReason == EpisodeCloseReason.SensitivityOff));
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
                    p.ServiceId == e.ServiceId
                    && p.Fingerprint == e.Fingerprint
                    && p.Id.CompareTo(e.Id) < 0),
                // Actions land only on the newest Episode of a kind, so the newest earlier
                // Episode holding an acknowledgement also holds the kind's newest one.
                EarlierAck = dbContext.Episodes
                    .Where(p => p.ServiceId == e.ServiceId
                        && p.Fingerprint == e.Fingerprint
                        && p.Id.CompareTo(e.Id) < 0
                        && p.AcknowledgedByUserId != null)
                    .OrderByDescending(p => p.Id)
                    .Select(p => new { p.AcknowledgedByUserId, p.AcknowledgedAt })
                    .FirstOrDefault(),
                // Keyed on the new table's primary key, so this is a lookup, not a scan.
                FingerprintQuietWindowMinutes = dbContext.FingerprintQuietWindows
                    .Where(w => w.ServiceId == e.ServiceId && w.Fingerprint == e.Fingerprint)
                    .Select(w => (int?)w.QuietWindowMinutes)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var names = await userNames.ResolveAsync(
            rows.SelectMany(r => new[]
                {
                    r.Episode.AcknowledgedByUserId, r.Episode.SolvedByUserId,
                    r.EarlierAck?.AcknowledgedByUserId,
                })
                .OfType<Guid>()
                .ToHashSet(),
            cancellationToken);

        var items = rows.Select(r => new EpisodeDto(
            r.Episode.Id, r.Episode.ApplicationId.Value, r.Episode.ServiceId.Value,
            r.Episode.Fingerprint, r.Episode.State, r.Episode.OpenedAt, r.Episode.ClosedAt,
            r.Episode.LastMatchAt, r.Episode.ErrorCount, r.Episode.WarnCount,
            r.Episode.FirstLogId, r.Episode.FirstLogTimestamp, r.Episode.FirstLogSeverity,
            r.Episode.FirstLogBody,
            r.Episode.AcknowledgedAt, NameOf(names, r.Episode.AcknowledgedByUserId),
            r.Episode.SolvedAt, NameOf(names, r.Episode.SolvedByUserId),
            r.EarlierAck?.AcknowledgedAt, NameOf(names, r.EarlierAck?.AcknowledgedByUserId),
            r.PriorCount, r.FingerprintQuietWindowMinutes)).ToList();

        return Results.Ok(new ListEpisodesResponse(items));
    }

    /// <summary>Null when nobody holds the mark; a deleted User leaves the timestamp with no name.</summary>
    internal static string? NameOf(IReadOnlyDictionary<Guid, string> names, Guid? userId) =>
        userId is { } id && names.TryGetValue(id, out var name) ? name : null;

    // Access's claim helper is internal to Access; the two lines are cheaper than a contract.
    private static Guid? GetUserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
