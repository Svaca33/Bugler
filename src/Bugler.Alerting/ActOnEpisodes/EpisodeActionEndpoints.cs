using System.Security.Claims;
using Bugler.Access.Contracts;
using Bugler.Alerting.Episodes;
using Bugler.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Alerting.ActOnEpisodes;

/// <summary>
/// The two human hands on an Episode (see CONTEXT.md: Acknowledged, Solved), open to anyone
/// within whose Visibility Scope it falls — the audience the grants already chose (ADR 0003).
/// An Episode outside that scope 404s: not yours to see, not yours to know about. All three
/// hands land only on the newest Episode of its kind (ADR 0005) — older ones are history.
/// Every hand that acts writes its Journal entry (ADR 0006); one that changes nothing writes
/// nothing and answers 204 all the same.
/// </summary>
internal static class EpisodeActionEndpoints
{
    public static Task<IResult> Acknowledge(
        Guid id,
        ClaimsPrincipal principal,
        AlertingDbContext dbContext,
        IReadVisibility readVisibility,
        IRequestLanguage requestLanguage,
        CancellationToken cancellationToken) =>
        Act(id, principal, dbContext, readVisibility, requestLanguage, cancellationToken,
            static (episode, userId, now) => episode.Acknowledge(userId, now),
            static messages => messages.SolvedEpisodeNeverAcknowledged,
            JournalEntryKind.Acknowledged);

    public static Task<IResult> Unacknowledge(
        Guid id,
        ClaimsPrincipal principal,
        AlertingDbContext dbContext,
        IReadVisibility readVisibility,
        IRequestLanguage requestLanguage,
        CancellationToken cancellationToken) =>
        Act(id, principal, dbContext, readVisibility, requestLanguage, cancellationToken,
            static (episode, _, _) => episode.Unacknowledge(), // Withdrawing nothing is nothing.
            static messages => messages.WithdrawingNeverRefuses,
            JournalEntryKind.Withdrawn);

    public static Task<IResult> Solve(
        Guid id,
        ClaimsPrincipal principal,
        AlertingDbContext dbContext,
        IReadVisibility readVisibility,
        IRequestLanguage requestLanguage,
        CancellationToken cancellationToken) =>
        Act(id, principal, dbContext, readVisibility, requestLanguage, cancellationToken,
            static (episode, userId, now) => episode.Solve(userId, now),
            static messages => messages.EpisodeAlreadySolved,
            JournalEntryKind.Solved,
            // Solve consumes every acknowledgement its kind of trouble has ever held (ADR 0005).
            // Each Episode it strips gets a Withdrawn entry by the solver's hand (ADR 0006) —
            // every Journal stays complete on its own.
            after: static async (dbContext, episode, userId, now, cancellationToken) =>
            {
                var acknowledged = await dbContext.Episodes
                    .Where(e => e.ServiceId == episode.ServiceId
                        && e.Fingerprint == episode.Fingerprint
                        && e.AcknowledgedByUserId != null)
                    .ToListAsync(cancellationToken);
                foreach (var earlier in acknowledged)
                {
                    // The WHERE ran on the database, so the solved Episode itself comes back too
                    // (its consumption is unsaved yet) — as the tracked instance already stripped
                    // by Solve. Nothing here keeps its consumption implied by the Solved entry.
                    if (earlier.Unacknowledge() is HandOutcome.Acted)
                    {
                        dbContext.JournalEntries.Add(new JournalEntry
                        {
                            EpisodeId = earlier.Id,
                            Kind = JournalEntryKind.Withdrawn,
                            UserId = userId,
                            At = now,
                        });
                    }
                }
            });

    /// <summary>One shape for all three: load inside the scope, act, journal the act, 409 when refused.</summary>
    private static async Task<IResult> Act(
        Guid id,
        ClaimsPrincipal principal,
        AlertingDbContext dbContext,
        IReadVisibility readVisibility,
        IRequestLanguage requestLanguage,
        CancellationToken cancellationToken,
        Func<Episode, Guid, DateTimeOffset, HandOutcome> act,
        Func<AlertingMessages, string> refusal,
        JournalEntryKind kind,
        Func<AlertingDbContext, Episode, Guid, DateTimeOffset, CancellationToken, Task>? after = null)
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
            var messages = AlertingMessages.For(await requestLanguage.GetAsync(cancellationToken));
            return Results.Conflict(messages.ActionBelongsToNewestEpisode);
        }

        var now = DateTimeOffset.UtcNow;
        switch (act(episode, userId, now))
        {
            case HandOutcome.Refused:
                return Results.Conflict(
                    refusal(AlertingMessages.For(await requestLanguage.GetAsync(cancellationToken))));
            case HandOutcome.Nothing:
                return Results.NoContent();
        }

        dbContext.JournalEntries.Add(new JournalEntry
        {
            EpisodeId = episode.Id,
            Kind = kind,
            UserId = userId,
            At = now,
        });

        if (after is not null)
        {
            await after(dbContext, episode, userId, now, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    // Access's claim helper is internal to Access; the two lines are cheaper than a contract.
    private static Guid? GetUserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
