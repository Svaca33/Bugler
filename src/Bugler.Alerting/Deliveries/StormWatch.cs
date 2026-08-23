using Bugler.Alerting.DetectEpisodes;
using Bugler.Alerting.Episodes;
using Bugler.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Alerting.Deliveries;

/// <summary>
/// The mailbox's guard (see CONTEXT.md: Storm). More kinds of trouble opening in one Episode
/// Scope at once than anybody can read used to be held back by a cap on open Episodes; ADR 0034
/// moved the cap off the table and onto the messages, because what it really guarded was the
/// mailbox. The Episodes open unhindered and every one of them is there to be seen — it is the
/// Alerts that fold into a single digest naming how many.
///
/// One of these is opened per poll page and knows two things the page cannot see for itself: how
/// many Episodes each Scope has already opened inside the window, and whether a digest has
/// already gone out for it.
/// </summary>
internal sealed class StormWatch
{
    private readonly int _threshold;
    private readonly Dictionary<string, int> _openedInWindow;
    private readonly HashSet<string> _alreadyDigested;

    /// <summary>The newest folded Episode of each storming Scope — what its digest is attached to.</summary>
    private readonly Dictionary<string, Episode> _folded = [];

    private StormWatch(
        int threshold, Dictionary<string, int> openedInWindow, HashSet<string> alreadyDigested)
    {
        _threshold = threshold;
        _openedInWindow = openedInWindow;
        _alreadyDigested = alreadyDigested;
    }

    public static async Task<StormWatch> OpenAsync(
        AlertingDbContext dbContext,
        DetectionDecisions decisions,
        AlertingOptions options,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var opening = decisions.Scopes
            .Where(d => d.OpensWith is not null)
            .Select(d => d.ScopeKey)
            .Distinct()
            .ToList();
        if (opening.Count == 0)
        {
            return new StormWatch(options.StormThreshold, [], []);
        }

        var since = now - TimeSpan.FromMinutes(options.StormWindowMinutes);
        var openedInWindow = await dbContext.Episodes
            .Where(e => opening.Contains(e.ScopeKey) && e.Watch == Watch.Logs && e.OpenedAt >= since)
            .GroupBy(e => e.ScopeKey)
            .Select(group => new { ScopeKey = group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.ScopeKey, row => row.Count, cancellationToken);

        // One digest per Scope per window: a Storm that keeps blowing says so once.
        var digested = await dbContext.Episodes
            .Where(e => opening.Contains(e.ScopeKey)
                && dbContext.Deliveries.Any(d => d.EpisodeId == e.Id
                    && d.Kind == DeliveryKind.StormDigest
                    && d.CreatedAt >= since))
            .Select(e => e.ScopeKey)
            .Distinct()
            .ToListAsync(cancellationToken);

        return new StormWatch(options.StormThreshold, openedInWindow, digested.ToHashSet());
    }

    /// <summary>
    /// Counts one more Episode opening in this Scope and says whether its Alerts fold. Called
    /// once per opening — the count is what decides, so asking twice would over-count.
    /// </summary>
    public bool FoldsAlertsOf(string scopeKey)
    {
        var opened = _openedInWindow.GetValueOrDefault(scopeKey) + 1;
        _openedInWindow[scopeKey] = opened;
        return opened > _threshold;
    }

    /// <summary>The Episode whose Alerts were folded; the last one of a Scope carries its digest.</summary>
    public void Fold(Episode episode) => _folded[episode.ScopeKey] = episode;

    /// <summary>
    /// Owes each storming Scope its one digest, at the end of the page rather than at the first
    /// fold — the number it names is then the number that actually opened, not the number that
    /// happened to have opened when the fold began.
    /// </summary>
    public async Task OweDigestsAsync(
        AlertingDbContext dbContext,
        bool mailEnabled,
        IReadOnlySet<ApplicationId> applicationsWithWebhook,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        foreach (var (scopeKey, episode) in _folded)
        {
            if (!_alreadyDigested.Add(scopeKey))
            {
                continue;
            }

            await AlertsOwed.EnqueueStormDigestAsync(
                dbContext, episode, _openedInWindow[scopeKey], mailEnabled,
                applicationsWithWebhook.Contains(episode.ApplicationId), now, cancellationToken);
        }

        _folded.Clear();
    }
}
