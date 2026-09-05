using Bugler.Access.Contracts;
using Bugler.Alerting.Episodes;
using Bugler.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Alerting.ListEpisodes;

/// <summary>
/// Every narrowing the list and its counts share — everything except state, which is the list's
/// filter but the counts' own axis. One place, so the rail's numbers can never drift from the rows.
/// </summary>
internal static class EpisodeFilter
{
    /// <summary>"none" (nobody holds the mark) or "me" (the caller does); anything else is a caller error.</summary>
    public static bool IsValidAcknowledged(string? acknowledged) =>
        acknowledged is null or "none" or "me";

    /// <summary>
    /// Which of the caller's two sets a listing starts from. A request that names its own source —
    /// an Application, or Services within one — is answered from the Visibility Scope; anything
    /// wider is answered through the Focus (Access ADR 0004). Kept here beside the filter it feeds
    /// so the list, its counts and the per-service board cannot each decide it differently.
    /// </summary>
    public static ValueTask<IReadOnlyCollection<ApplicationId>?> ApplicationsForAsync(
        IReadVisibility visibility,
        IReadApplicationFocus focus,
        Guid? applicationId,
        Guid[]? serviceId,
        CancellationToken cancellationToken) =>
        applicationId is null && serviceId is not { Length: > 0 }
            ? focus.GetFocusedApplicationsAsync(cancellationToken)
            : visibility.GetVisibleApplicationsAsync(cancellationToken);

    public static IQueryable<Episode> Apply(
        this IQueryable<Episode> query,
        AlertingDbContext dbContext,
        IReadOnlyCollection<ApplicationId>? visible,
        Guid? applicationId,
        Guid[]? serviceId,
        string? scopeKey,
        string? fingerprint,
        DateTimeOffset? from,
        string? q,
        string? acknowledged,
        Guid callerId)
    {
        if (visible is not null)
        {
            var visibleIds = visible.ToList();
            query = query.Where(e => visibleIds.Contains(e.ApplicationId));
        }

        if (applicationId is { } application)
        {
            var id = new ApplicationId(application);
            query = query.Where(e => e.ApplicationId == id);
        }

        if (serviceId is { Length: > 0 })
        {
            // An Episode has no single Service (ADR 0034): "in this Service" means the Service
            // put something into it, which is what a Participation records.
            var ids = serviceId.Select(id => new ServiceId(id)).ToList();
            query = query.Where(e => dbContext.Participations.Any(
                p => p.EpisodeId == e.Id && ids.Contains(p.ServiceId)));
        }

        if (scopeKey is not null)
        {
            // Opaque to whoever passes it, exactly as the Fingerprint is: the client echoes back
            // what an earlier answer carried.
            query = query.Where(e => e.ScopeKey == scopeKey);
        }

        if (fingerprint is not null)
        {
            // A narrowing the caller asked for, not an answer to "which kind is this" — that
            // question carries the Watch (Alerting ADR 0011), and the row says which Watch it
            // was. Nobody has yet wanted to ask for one Watch's alone, so nothing offers it.
            query = query.Where(e => e.Fingerprint == fingerprint);
        }

        if (from is { } opened)
        {
            query = query.Where(e => e.OpenedAt >= opened);
        }

        if (!string.IsNullOrEmpty(q))
        {
            var pattern = "%" + q.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_") + "%";
            // The Title is what a person reads (ADR 0033), so it is what a person searches.
            query = query.Where(e =>
                EF.Functions.ILike(e.FirstMatchDetail!, pattern, @"\")
                || EF.Functions.ILike(e.Title, pattern, @"\"));
        }

        return acknowledged switch
        {
            "none" => query.Where(e => e.AcknowledgedByUserId == null),
            "me" => query.Where(e => e.AcknowledgedByUserId == callerId),
            _ => query,
        };
    }

    /// <summary>
    /// The everyday view: a filed Episode is out of it (see CONTEXT.md: Archived). Deliberately
    /// not part of <see cref="Apply"/> — the rail hides the filed ones while still counting
    /// them, because an observability tool must never let "hidden" read as "absent" (the same
    /// principle as the Focus, Access ADR 0004).
    /// </summary>
    public static IQueryable<Episode> WhereNotArchived(this IQueryable<Episode> query) =>
        query.Where(e => e.ArchivedAt == null);

    /// <summary>The filed ones alone — the other half of the mark, for a caller who asks for it.</summary>
    public static IQueryable<Episode> WhereArchived(this IQueryable<Episode> query) =>
        query.Where(e => e.ArchivedAt != null);

    /// <summary>
    /// Keeps only each kind of trouble's latest Episode — the face the grouped list shows. The
    /// face is absolute: newest of its (Episode Scope, Watch, Fingerprint) over
    /// <paramref name="everything"/>, regardless of any narrowing already applied, so this
    /// composes with Apply in either order. UUIDv7 ids compare bytewise in PostgreSQL, so "newer"
    /// is one id comparison.
    /// </summary>
    public static IQueryable<Episode> WhereLatestPerFingerprint(
        this IQueryable<Episode> query, IQueryable<Episode> everything) =>
        query.Where(e => !everything.Any(n =>
            n.ScopeKey == e.ScopeKey
            && n.Watch == e.Watch
            && n.Fingerprint == e.Fingerprint
            && n.Id.CompareTo(e.Id) > 0));
}
