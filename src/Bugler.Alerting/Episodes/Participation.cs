using Bugler.SharedKernel;

namespace Bugler.Alerting.Episodes;

/// <summary>
/// What one Service running one version contributed to an Episode (see CONTEXT.md: Participation)
/// — when it first and last fell in, and how much. The answer to "is it still happening on the
/// version we just shipped, and is it every deployment or only one".
///
/// The version is the Match's own <c>service.version</c>, not the release ledger's: that ledger
/// holds stragglers for thirty minutes and passes over quick rollbacks (ADR 0016), so during a
/// rolling deploy it reports one version while two are demonstrably running — the exact moment
/// this question gets asked.
/// </summary>
public sealed class Participation
{
    /// <summary>
    /// How many a single Episode may hold. A sender that puts a build id into
    /// <c>service.version</c> would otherwise open one per process; past the ceiling the Matches
    /// still count on the Episode and no further Participation is opened.
    /// </summary>
    public const int MaxPerEpisode = 50;

    /// <summary>What the column holds — <c>service.version</c> is a version, not a changelog.</summary>
    public const int MaxVersionLength = 200;

    /// <summary>
    /// A surrogate, because the real key — Episode, Service and version — has a nullable member:
    /// a sender that declares no version is one participant, not one per Match. The unique index
    /// treats those nulls as equal and is the key the domain actually means.
    /// </summary>
    public required Guid Id { get; init; }

    public required Guid EpisodeId { get; init; }
    public required ServiceId ServiceId { get; init; }

    /// <summary>The version this Service declared when it fell in; null where it declared none.</summary>
    public string? Version { get; init; }

    public required DateTimeOffset FirstAt { get; init; }
    public DateTimeOffset LastAt { get; set; }
    public int ErrorCount { get; set; }
    public int WarnCount { get; set; }
}
