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

    public static IQueryable<Episode> Apply(
        this IQueryable<Episode> query,
        IReadOnlyCollection<ApplicationId>? visible,
        Guid? applicationId,
        Guid[]? serviceId,
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
            var ids = serviceId.Select(id => new ServiceId(id)).ToList();
            query = query.Where(e => ids.Contains(e.ServiceId));
        }

        if (fingerprint is not null)
        {
            query = query.Where(e => e.Fingerprint == fingerprint);
        }

        if (from is { } opened)
        {
            query = query.Where(e => e.OpenedAt >= opened);
        }

        if (!string.IsNullOrEmpty(q))
        {
            var pattern = "%" + q.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_") + "%";
            query = query.Where(e => EF.Functions.ILike(e.FirstMatchDetail!, pattern, @"\"));
        }

        return acknowledged switch
        {
            "none" => query.Where(e => e.AcknowledgedByUserId == null),
            "me" => query.Where(e => e.AcknowledgedByUserId == callerId),
            _ => query,
        };
    }

    /// <summary>
    /// Keeps only each kind of trouble's latest Episode — the face the grouped list shows. The
    /// face is absolute: newest of its (Service, Fingerprint) over <paramref name="everything"/>,
    /// regardless of any narrowing already applied, so this composes with Apply in either order.
    /// UUIDv7 ids compare bytewise in PostgreSQL, so "newer" is one id comparison.
    /// </summary>
    public static IQueryable<Episode> WhereLatestPerFingerprint(
        this IQueryable<Episode> query, IQueryable<Episode> everything) =>
        query.Where(e => !everything.Any(n =>
            n.ServiceId == e.ServiceId
            && n.Fingerprint == e.Fingerprint
            && n.Id.CompareTo(e.Id) > 0));
}
