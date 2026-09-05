using Bugler.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Alerting.DeleteKindOfTrouble;

/// <summary>
/// The Deletion of one kind of trouble (see CONTEXT.md: Deletion; Alerting ADR 0012), addressed
/// through any Episode of it: the Episode names the (Episode Scope, Watch, Fingerprint) the kind
/// is told by (Alerting ADR 0011), and every Episode sharing them goes in one transaction.
///
/// It reaches the kind, never a single Episode, because several answers an Episode gives are
/// claims about its kind rather than about itself — how often it recurred before, who
/// acknowledged it earlier, whether a Solved Proposal has been overtaken, which Episode is the
/// kind's face in the grouped list. Delete one Episode out of a surviving history and every one of
/// those answers silently changes for a reader who could never have seen the deleted Episode.
/// Deleting the kind entire leaves nobody behind to answer wrongly.
///
/// Refused unless every Episode of the kind is closed and Archived: filing is the reversible step
/// that must precede the irreversible one, and an open Episode could not usefully be deleted
/// anyway — detection would reopen the kind on its next Match.
/// </summary>
internal static class KindOfTroubleDeletionEndpoint
{
    public static async Task<IResult> Delete(
        Guid id,
        AlertingDbContext dbContext,
        IRequestLanguage requestLanguage,
        CancellationToken cancellationToken)
    {
        // One transaction from the first read to the last delete: the preconditions are judged
        // and the kind removed against one consistent view, and a refusal or failure part-way
        // leaves the record exactly as it was (disposal rolls back whatever was not committed).
        // ExecuteDelete starts no transaction of its own.
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var named = await dbContext.Episodes.AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new { e.ScopeKey, e.Watch, e.Fingerprint })
            .FirstOrDefaultAsync(cancellationToken);
        if (named is null)
        {
            return Results.NotFound();
        }

        var kind = dbContext.Episodes.Where(e =>
            e.ScopeKey == named.ScopeKey
            && e.Watch == named.Watch
            && e.Fingerprint == named.Fingerprint);

        if (await Standing(kind, cancellationToken) is { } refusal)
        {
            return await Refuse(refusal, requestLanguage, cancellationToken);
        }

        // The Participations, Journal entries, Readings and owed Deliveries hang off the Episode
        // by foreign key and cascade with it. The Journal is append-only by definition (ADR 0006:
        // entries die only with their Episode) — this is the one operation in Alerting permitted
        // to destroy it, and that is exactly why it is an Admin's act and asks for the Archived
        // mark first. Only what passed the bar is deleted, so a hand that lands between the check
        // and the delete — detection opening a new Episode, a person lifting the mark — leaves
        // its Episode standing for the check below to find.
        await kind
            .Where(e => e.ClosedAt != null && e.ArchivedAt != null)
            .ExecuteDeleteAsync(cancellationToken);

        // Anything of the kind still standing means the bar was crossed mid-click. It must not
        // be orphaned of its history either, so the whole thing rolls back and the click is
        // refused with the sentence it would have got a moment earlier.
        if (await Standing(kind, cancellationToken) is { } crossed)
        {
            await transaction.RollbackAsync(cancellationToken);
            return await Refuse(crossed, requestLanguage, cancellationToken);
        }

        // The Quiet Window override is keyed on the kind and holds no foreign key to any Episode,
        // so it goes explicitly or it is orphaned forever.
        await dbContext.FingerprintQuietWindows
            .Where(w => w.ScopeKey == named.ScopeKey
                && w.Watch == named.Watch
                && w.Fingerprint == named.Fingerprint)
            .ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return Results.NoContent();
    }

    /// <summary>
    /// What still bars the kind from Deletion, or null when nothing does: an open Episode is the
    /// sharper refusal, because filing it is not even an option yet.
    /// </summary>
    private static async Task<Func<AlertingMessages, string>?> Standing(
        IQueryable<Episodes.Episode> kind, CancellationToken cancellationToken)
    {
        if (await kind.AnyAsync(e => e.ClosedAt == null, cancellationToken))
        {
            return static messages => messages.KindStillHasAnOpenEpisode;
        }

        if (await kind.AnyAsync(e => e.ArchivedAt == null, cancellationToken))
        {
            return static messages => messages.KindNotYetArchivedWhole;
        }

        return null;
    }

    private static async Task<IResult> Refuse(
        Func<AlertingMessages, string> sentence,
        IRequestLanguage requestLanguage,
        CancellationToken cancellationToken) =>
        Results.Conflict(
            sentence(AlertingMessages.For(await requestLanguage.GetAsync(cancellationToken))));
}
