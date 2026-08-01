using System.Security.Claims;
using Bugler.Access.Contracts;
using Bugler.Alerting.Episodes;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Alerting.ActOnEpisodes;

/// <summary>
/// The two human hands on an Episode (see CONTEXT.md: Acknowledged, Solved), open to anyone
/// within whose Visibility Scope it falls — the audience the grants already chose (ADR 0003).
/// An Episode outside that scope 404s: not yours to see, not yours to know about. All three
/// hands land only on the newest Episode of its kind (ADR 0005) — older ones are history.
/// </summary>
internal static class EpisodeActionEndpoints
{
    public static Task<IResult> Acknowledge(
        Guid id,
        ClaimsPrincipal principal,
        AlertingDbContext dbContext,
        IReadVisibility readVisibility,
        CancellationToken cancellationToken) =>
        Act(id, principal, dbContext, readVisibility, cancellationToken,
            (episode, userId, now) => episode.Acknowledge(userId, now)
                ? null
                : "A Solved Episode is never Acknowledged.");

    public static Task<IResult> Unacknowledge(
        Guid id,
        ClaimsPrincipal principal,
        AlertingDbContext dbContext,
        IReadVisibility readVisibility,
        CancellationToken cancellationToken) =>
        Act(id, principal, dbContext, readVisibility, cancellationToken,
            (episode, _, _) =>
            {
                episode.Unacknowledge(); // Idempotent: withdrawing nothing is nothing.
                return null;
            });

    public static Task<IResult> Solve(
        Guid id,
        ClaimsPrincipal principal,
        AlertingDbContext dbContext,
        IReadVisibility readVisibility,
        CancellationToken cancellationToken) =>
        Act(id, principal, dbContext, readVisibility, cancellationToken,
            (episode, userId, now) => episode.Solve(userId, now)
                ? null
                : "The Episode is already Solved; the verdict is rendered once.",
            // Solve consumes every acknowledgement its kind of trouble has ever held (ADR 0005):
            // the marks are live claims, not records, and the verdict ends the claim.
            after: static async (dbContext, episode, cancellationToken) =>
            {
                var acknowledged = await dbContext.Episodes
                    .Where(e => e.ServiceId == episode.ServiceId
                        && e.Fingerprint == episode.Fingerprint
                        && e.AcknowledgedByUserId != null)
                    .ToListAsync(cancellationToken);
                foreach (var earlier in acknowledged)
                {
                    earlier.Unacknowledge();
                }
            });

    /// <summary>One shape for all three: load inside the scope, act, 409 when the act refuses.</summary>
    private static async Task<IResult> Act(
        Guid id,
        ClaimsPrincipal principal,
        AlertingDbContext dbContext,
        IReadVisibility readVisibility,
        CancellationToken cancellationToken,
        Func<Episode, Guid, DateTimeOffset, string?> act,
        Func<AlertingDbContext, Episode, CancellationToken, Task>? after = null)
    {
        if (GetUserId(principal) is not { } userId)
        {
            return Results.Unauthorized();
        }

        var episode = await dbContext.Episodes
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

        // Ids are UUIDv7, so a newer Episode of the kind is one bytewise id comparison away.
        // This also nets the race where detection opens a new Episode mid-click: the stale
        // click 409s and the UI refetches, instead of a hand landing on history.
        var newerExists = await dbContext.Episodes.AnyAsync(n =>
            n.ServiceId == episode.ServiceId
            && n.Fingerprint == episode.Fingerprint
            && n.Id.CompareTo(episode.Id) > 0, cancellationToken);
        if (newerExists)
        {
            return Results.Conflict("The action belongs to the newest Episode of its kind.");
        }

        if (act(episode, userId, DateTimeOffset.UtcNow) is { } refusal)
        {
            return Results.Conflict(refusal);
        }

        if (after is not null)
        {
            await after(dbContext, episode, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    // Access's claim helper is internal to Access; the two lines are cheaper than a contract.
    private static Guid? GetUserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
