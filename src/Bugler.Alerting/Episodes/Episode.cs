using Bugler.SharedKernel;

namespace Bugler.Alerting.Episodes;

/// <summary>
/// One bounded stretch of trouble in one Service (see CONTEXT.md: Episode). Carries everything a
/// message about it needs — Deliveries never read the evidence again, so an Alert survives the
/// Purge of the Log Records that drove it.
/// </summary>
public sealed class Episode
{
    public required Guid Id { get; init; }
    public required ServiceId ServiceId { get; init; }

    /// <summary>Stored redundantly (immutable mapping) so visibility filtering and cascades need no catalog round-trip.</summary>
    public required ApplicationId ApplicationId { get; init; }

    /// <summary>Which watch found this trouble and keeps feeding it (see CONTEXT.md: Watch).</summary>
    public required Watch Watch { get; init; }

    /// <summary>The kind of trouble this Episode is about — what tells it apart from the Service's other Episodes.</summary>
    public required string Fingerprint { get; init; }

    /// <summary>Detection wall-clock, not the evidence's own claim — durations stay immune to sender clock skew.</summary>
    public required DateTimeOffset OpenedAt { get; init; }

    /// <summary>The Log Record the opening match was, where the Watch deals in Log Records; null where it does not.</summary>
    public long? FirstMatchLogId { get; init; }

    /// <summary>When the opening match happened by its own reckoning — the log's timestamp, the probe's moment.</summary>
    public required DateTimeOffset FirstMatchAt { get; init; }

    /// <summary>The opening match's Severity Band, where the Watch has one; null where it does not.</summary>
    public short? FirstMatchSeverity { get; init; }

    /// <summary>What the opening match said, in one line — a log's body, or what a probe got back.</summary>
    public string? FirstMatchDetail { get; init; }

    public int ErrorCount { get; set; }
    public int WarnCount { get; set; }

    /// <summary>Processing wall-clock of the newest match; the Quiet Window is measured from here.</summary>
    public DateTimeOffset LastMatchAt { get; set; }

    public DateTimeOffset? ClosedAt { get; set; }

    /// <summary>How the stretch stopped taking matches; the display state is derived, never stored twice (ADR 0003).</summary>
    public EpisodeCloseReason? CloseReason { get; set; }

    public DateTimeOffset? AcknowledgedAt { get; private set; }
    public Guid? AcknowledgedByUserId { get; private set; }

    public DateTimeOffset? SolvedAt { get; private set; }
    public Guid? SolvedByUserId { get; private set; }

    /// <summary>Not mapped (no setter): Solved wins, then whether — and how — the stretch ended.</summary>
    public EpisodeState State =>
        SolvedAt is not null ? EpisodeState.Solved
        : ClosedAt is null ? EpisodeState.Open
        : CloseReason is EpisodeCloseReason.QuietWindow ? EpisodeState.Quieted
        : EpisodeState.Muted;

    /// <summary>
    /// Takes the Episode on — or over: one slot, last hand wins (see CONTEXT.md: Acknowledged).
    /// Refused on a Solved Episode, which is never Acknowledged; the holder re-acknowledging is
    /// not an act — the mark keeps its original moment, so the Journal stays its full explanation.
    /// </summary>
    public HandOutcome Acknowledge(Guid userId, DateTimeOffset now)
    {
        if (SolvedAt is not null)
        {
            return HandOutcome.Refused;
        }

        if (AcknowledgedByUserId == userId)
        {
            return HandOutcome.Nothing;
        }

        AcknowledgedByUserId = userId;
        AcknowledgedAt = now;
        return HandOutcome.Acted;
    }

    /// <summary>Withdraws the acknowledgement — the live claim ends; what happened is the Journal's to tell.</summary>
    public HandOutcome Unacknowledge()
    {
        if (AcknowledgedByUserId is null)
        {
            return HandOutcome.Nothing;
        }

        AcknowledgedByUserId = null;
        AcknowledgedAt = null;
        return HandOutcome.Acted;
    }

    /// <summary>
    /// The terminal human verdict (see CONTEXT.md: Solved): ends an open Episode on the spot and
    /// consumes any acknowledgement. Refused when already Solved — the verdict is rendered once.
    /// </summary>
    public HandOutcome Solve(Guid userId, DateTimeOffset now)
    {
        if (SolvedAt is not null)
        {
            return HandOutcome.Refused;
        }

        if (ClosedAt is null)
        {
            ClosedAt = now;
            CloseReason = EpisodeCloseReason.Solved;
        }

        SolvedByUserId = userId;
        SolvedAt = now;
        Unacknowledge();
        return HandOutcome.Acted;
    }
}
