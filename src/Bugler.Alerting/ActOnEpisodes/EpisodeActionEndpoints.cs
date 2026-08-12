using System.Security.Claims;
using Bugler.Access.Contracts;
using Bugler.Alerting.Deliveries;
using Bugler.Alerting.Episodes;
using Bugler.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Alerting.ActOnEpisodes;

/// <summary>
/// The human hands on an Episode (see CONTEXT.md: Acknowledged, Solved — and, since the machine
/// hand, the hands that answer its marks), open to anyone within whose Visibility Scope it falls
/// — the audience the grants already chose (ADR 0003). An Episode outside that scope 404s: not
/// yours to see, not yours to know about. Acknowledge, withdraw and Solve land only on the
/// newest Episode of its kind (ADR 0005) — older ones are history; the hands that sweep a
/// machine mark aside reach overtaken Episodes too, because the marks age in place. Every hand
/// that acts writes its Journal entry (ADR 0006); one that changes nothing writes nothing and
/// answers 204 all the same.
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

    /// <summary>
    /// A person rejecting the Solved Proposal: it goes, and the claim goes with it. Reaches an
    /// overtaken proposal too — saying no to history is still worth a Journal line.
    /// </summary>
    public static Task<IResult> RejectProposal(
        Guid id,
        ClaimsPrincipal principal,
        AlertingDbContext dbContext,
        IReadVisibility readVisibility,
        IRequestLanguage requestLanguage,
        CancellationToken cancellationToken) =>
        Act(id, principal, dbContext, readVisibility, requestLanguage, cancellationToken,
            static (episode, _, _) => episode.RejectProposal(),
            static messages => messages.WithdrawingNeverRefuses,
            JournalEntryKind.ProposalRejected,
            requireNewest: false,
            journalDelegation: static episode => episode.ProposalByDelegationId);

    /// <summary>
    /// A person sweeping the Resignation aside: machines may claim again. The messages it still
    /// owes lapse — a call for a human hand arriving after one already answered is stale panic.
    /// </summary>
    public static Task<IResult> DismissResignation(
        Guid id,
        ClaimsPrincipal principal,
        AlertingDbContext dbContext,
        IReadVisibility readVisibility,
        IRequestLanguage requestLanguage,
        CancellationToken cancellationToken) =>
        Act(id, principal, dbContext, readVisibility, requestLanguage, cancellationToken,
            static (episode, _, _) => episode.DismissResignation(),
            static messages => messages.WithdrawingNeverRefuses,
            JournalEntryKind.ResignationDismissed,
            requireNewest: false,
            journalDelegation: static episode => episode.ResignedByDelegationId,
            after: static async (dbContext, episode, _, now, cancellationToken) =>
            {
                var pending = await dbContext.Deliveries
                    .Where(d => d.EpisodeId == episode.Id
                        && d.Kind == DeliveryKind.Resignation
                        && d.DeliveredAt == null && d.LapsedAt == null)
                    .ToListAsync(cancellationToken);
                foreach (var delivery in pending)
                {
                    delivery.LapsedAt = now;
                    delivery.LastError = "The resignation was dismissed before this message left.";
                }
            });

    /// <summary>The human hand always wins: withdraws the Machine Claim, whoever's it is.</summary>
    public static Task<IResult> WithdrawClaim(
        Guid id,
        ClaimsPrincipal principal,
        AlertingDbContext dbContext,
        IReadVisibility readVisibility,
        IRequestLanguage requestLanguage,
        CancellationToken cancellationToken) =>
        Act(id, principal, dbContext, readVisibility, requestLanguage, cancellationToken,
            static (episode, _, _) =>
                episode.ShedClaim() is not null ? HandOutcome.Acted : HandOutcome.Nothing,
            static messages => messages.WithdrawingNeverRefuses,
            JournalEntryKind.ClaimDisplaced,
            requireNewest: false,
            journalDelegation: static episode => episode.ClaimedByDelegationId);

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

    /// <summary>One shape for every hand: load inside the scope, act, journal the act, 409 when refused.</summary>
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
        Func<AlertingDbContext, Episode, Guid, DateTimeOffset, CancellationToken, Task>? after = null,
        bool requireNewest = true,
        Func<Episode, Guid?>? journalDelegation = null)
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
        // click 409s and the UI refetches, instead of a hand landing on history. The hands that
        // sweep an overtaken machine mark aside skip it: those marks age in place.
        if (requireNewest)
        {
            var newerExists = await dbContext.Episodes.AnyAsync(n =>
                n.ServiceId == episode.ServiceId
                && n.Fingerprint == episode.Fingerprint
                && n.Id.CompareTo(episode.Id) > 0, cancellationToken);
            if (newerExists)
            {
                var messages = AlertingMessages.For(await requestLanguage.GetAsync(cancellationToken));
                return Results.Conflict(messages.ActionBelongsToNewestEpisode);
            }
        }

        // Read before the act: what the hand is about to land over, for the Journal's telling.
        var claimBefore = episode is { ClaimedByDelegationId: { } heldBy, ClaimedByUserId: not null }
            ? (Guid?)heldBy
            : null;
        var delegationForEntry = journalDelegation?.Invoke(episode);

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
            DelegationId = delegationForEntry,
            At = now,
        });

        // A human hand that shed a machine claim on its way in gets the displacement journaled
        // beside it — one line naming both hands — unless the displacement was the act itself.
        if (kind is not JournalEntryKind.ClaimDisplaced
            && claimBefore is { } displaced
            && episode.ClaimedByDelegationId is null)
        {
            dbContext.JournalEntries.Add(new JournalEntry
            {
                EpisodeId = episode.Id,
                Kind = JournalEntryKind.ClaimDisplaced,
                UserId = userId,
                DelegationId = displaced,
                At = now,
            });
        }

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
