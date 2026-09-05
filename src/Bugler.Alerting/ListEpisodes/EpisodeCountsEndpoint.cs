using System.Security.Claims;
using Bugler.Access.Contracts;
using Bugler.Alerting.Episodes;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Alerting.ListEpisodes;

/// <summary>
/// Proposals and Resignations are counted only where they stand — on the newest Episode of
/// their kind; an overtaken mark is history, not a call for a verdict. Archived stands beside
/// the four lifecycle states rather than among them (see CONTEXT.md: Archived): it is a mark on
/// top of a state, and it is counted even while it is hidden, so nobody reads "hidden" as
/// "absent".
/// </summary>
public sealed record EpisodeCountsResponse(
    int Open, int Quieted, int Solved, int Muted, int Proposals, int Resignations, int Archived);

internal static class EpisodeCountsEndpoint
{
    /// <summary>
    /// How many Episodes sit in each lifecycle state under the current filters — the rail's
    /// numbers and the nav badge without paging the table. State is never a filter here: the
    /// states are the axis the counts break down over.
    /// </summary>
    public static async Task<IResult> Handle(
        Guid? applicationId,
        Guid[]? serviceId,
        string? scopeKey,
        string? fingerprint,
        DateTimeOffset? from,
        string? q,
        string? acknowledged,
        bool? latestPerFingerprint,
        bool? includeArchived,
        ClaimsPrincipal principal,
        AlertingDbContext dbContext,
        IReadVisibility readVisibility,
        IReadApplicationFocus readFocus,
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

        // The badge counts the same server the list shows, so it starts from the same set
        // (Access ADR 0004).
        var visible = await EpisodeFilter.ApplicationsForAsync(
            readVisibility, readFocus, applicationId, serviceId, cancellationToken);
        if (visible is { Count: 0 })
        {
            return Results.Ok(new EpisodeCountsResponse(0, 0, 0, 0, 0, 0, 0));
        }

        // One pass over the filtered set: each state as its defining predicate (ADR 0003 — the
        // state is derived, never stored), folded into COUNT FILTER by the provider.
        var filtered = dbContext.Episodes.AsNoTracking()
            .Apply(dbContext, visible, applicationId, serviceId, scopeKey, fingerprint, from, q,
                acknowledged, callerId);

        if (latestPerFingerprint == true)
        {
            // Counting kinds of trouble by the state of their face — the same rows the grouped
            // list shows, so the rail's numbers can never drift from the table.
            filtered = filtered.WhereLatestPerFingerprint(dbContext.Episodes);
        }

        // How many are filed away under these filters — asked of the whole set, because the rail
        // shows this number precisely while the rows are hidden. The state counts then break
        // down what the list actually shows, filed ones in or out with it.
        var archived = await filtered.CountAsync(e => e.ArchivedAt != null, cancellationToken);
        if (includeArchived != true)
        {
            filtered = filtered.WhereNotArchived();
        }

        var counts = await filtered
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Open = g.Count(e => e.ClosedAt == null),
                Quieted = g.Count(e =>
                    e.SolvedAt == null && e.CloseReason == EpisodeCloseReason.QuietWindow),
                Solved = g.Count(e => e.SolvedAt != null),
                // Muted covers every close that is neither Quieted nor Solved: the Watch turned
                // off, and the Fingerprint Rule or Episode Scope changed under it (Regrouped).
                Muted = g.Count(e => e.SolvedAt == null && e.ClosedAt != null
                    && e.CloseReason != EpisodeCloseReason.QuietWindow),
            })
            .FirstOrDefaultAsync(cancellationToken);

        // Standing machine marks awaiting a person: counted apart because "newest of its kind"
        // is a sibling question the state axis never asks.
        var proposals = await filtered.CountAsync(e =>
            e.ProposedAt != null && !dbContext.Episodes.Any(p =>
                p.ScopeKey == e.ScopeKey
                && p.Watch == e.Watch
                && p.Fingerprint == e.Fingerprint
                && p.Id.CompareTo(e.Id) > 0), cancellationToken);
        var resignations = await filtered.CountAsync(e =>
            e.ResignedAt != null && !dbContext.Episodes.Any(p =>
                p.ScopeKey == e.ScopeKey
                && p.Watch == e.Watch
                && p.Fingerprint == e.Fingerprint
                && p.Id.CompareTo(e.Id) > 0), cancellationToken);

        return Results.Ok(counts is null
            ? new EpisodeCountsResponse(0, 0, 0, 0, proposals, resignations, archived)
            : new EpisodeCountsResponse(
                counts.Open, counts.Quieted, counts.Solved, counts.Muted, proposals, resignations,
                archived));
    }

    // Access's claim helper is internal to Access; the two lines are cheaper than a contract.
    private static Guid? GetUserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
