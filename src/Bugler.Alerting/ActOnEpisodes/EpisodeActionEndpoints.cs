using System.Security.Claims;
using Bugler.Access.Contracts;
using Bugler.Alerting.Episodes;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Alerting.ActOnEpisodes;

/// <summary>
/// The two human hands on an Episode (see CONTEXT.md: Acknowledged, Solved), open to anyone
/// within whose Visibility Scope it falls — the audience the grants already chose (ADR 0003).
/// An Episode outside that scope 404s: not yours to see, not yours to know about.
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
                : "The Episode is already Solved; the verdict is rendered once.");

    /// <summary>One shape for all three: load inside the scope, act, 409 when the act refuses.</summary>
    private static async Task<IResult> Act(
        Guid id,
        ClaimsPrincipal principal,
        AlertingDbContext dbContext,
        IReadVisibility readVisibility,
        CancellationToken cancellationToken,
        Func<Episode, Guid, DateTimeOffset, string?> act)
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

        if (act(episode, userId, DateTimeOffset.UtcNow) is { } refusal)
        {
            return Results.Conflict(refusal);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    // Access's claim helper is internal to Access; the two lines are cheaper than a contract.
    private static Guid? GetUserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
