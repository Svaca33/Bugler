using System.Security.Claims;
using Bugler.Access.Contracts;
using Bugler.Alerting.Episodes;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Alerting.ListEpisodes;

/// <summary>
/// Proposals and Resignations are counted only where they stand — on the newest Episode of
/// their kind; an overtaken mark is history, not a call for a verdict.
/// </summary>
public sealed record EpisodeCountsResponse(
    int Open, int Quieted, int Solved, int Muted, int Proposals, int Resignations);

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
        string? fingerprint,
        DateTimeOffset? from,
        string? q,
        string? acknowledged,
        bool? latestPerFingerprint,
        ClaimsPrincipal principal,
        AlertingDbContext dbContext,
        IReadVisibility readVisibility,
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
            return Results.Ok(new EpisodeCountsResponse(0, 0, 0, 0, 0, 0));
        }

        // One pass over the filtered set: each state as its defining predicate (ADR 0003 — the
        // state is derived, never stored), folded into COUNT FILTER by the provider.
        var filtered = dbContext.Episodes.AsNoTracking()
            .Apply(visible, applicationId, serviceId, fingerprint, from, q, acknowledged, callerId);

        if (latestPerFingerprint == true)
        {
            // Counting kinds of trouble by the state of their face — the same rows the grouped
            // list shows, so the rail's numbers can never drift from the table.
            filtered = filtered.WhereLatestPerFingerprint(dbContext.Episodes);
        }

        var counts = await filtered
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Open = g.Count(e => e.ClosedAt == null),
                Quieted = g.Count(e =>
                    e.SolvedAt == null && e.CloseReason == EpisodeCloseReason.QuietWindow),
                Solved = g.Count(e => e.SolvedAt != null),
                Muted = g.Count(e =>
                    e.SolvedAt == null && e.CloseReason == EpisodeCloseReason.WatchOff),
            })
            .FirstOrDefaultAsync(cancellationToken);

        // Standing machine marks awaiting a person: counted apart because "newest of its kind"
        // is a sibling question the state axis never asks.
        var proposals = await filtered.CountAsync(e =>
            e.ProposedAt != null && !dbContext.Episodes.Any(p =>
                p.ServiceId == e.ServiceId
                && p.Fingerprint == e.Fingerprint
                && p.Id.CompareTo(e.Id) > 0), cancellationToken);
        var resignations = await filtered.CountAsync(e =>
            e.ResignedAt != null && !dbContext.Episodes.Any(p =>
                p.ServiceId == e.ServiceId
                && p.Fingerprint == e.Fingerprint
                && p.Id.CompareTo(e.Id) > 0), cancellationToken);

        return Results.Ok(counts is null
            ? new EpisodeCountsResponse(0, 0, 0, 0, proposals, resignations)
            : new EpisodeCountsResponse(
                counts.Open, counts.Quieted, counts.Solved, counts.Muted, proposals, resignations));
    }

    // Access's claim helper is internal to Access; the two lines are cheaper than a contract.
    private static Guid? GetUserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
