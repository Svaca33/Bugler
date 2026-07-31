using Bugler.SharedKernel;

namespace Bugler.Alerting.Episodes;

/// <summary>
/// One bounded stretch of trouble in one Service (see CONTEXT.md: Episode). Carries everything a
/// message about it needs — Deliveries never read telemetry again, so an Alert survives the Purge
/// of the Log Records that drove it.
/// </summary>
public sealed class Episode
{
    public required Guid Id { get; init; }
    public required ServiceId ServiceId { get; init; }

    /// <summary>Stored redundantly (immutable mapping) so visibility filtering and cascades need no catalog round-trip.</summary>
    public required ApplicationId ApplicationId { get; init; }

    /// <summary>The kind of trouble this Episode is about — what tells it apart from the Service's other Episodes.</summary>
    public required string Fingerprint { get; init; }

    /// <summary>Detection wall-clock, not the log's own claim — durations stay immune to sender clock skew.</summary>
    public required DateTimeOffset OpenedAt { get; init; }

    public required long FirstLogId { get; init; }
    public required DateTimeOffset FirstLogTimestamp { get; init; }
    public required short FirstLogSeverity { get; init; }
    public string? FirstLogBody { get; init; }

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
    /// False on a Solved Episode, which is never Acknowledged.
    /// </summary>
    public bool Acknowledge(Guid userId, DateTimeOffset now)
    {
        if (SolvedAt is not null)
        {
            return false;
        }

        AcknowledgedByUserId = userId;
        AcknowledgedAt = now;
        return true;
    }

    /// <summary>Withdraws the acknowledgement — the live claim ends, no record remains.</summary>
    public void Unacknowledge()
    {
        AcknowledgedByUserId = null;
        AcknowledgedAt = null;
    }

    /// <summary>
    /// The terminal human verdict (see CONTEXT.md: Solved): ends an open Episode on the spot and
    /// consumes any acknowledgement. False when already Solved — the verdict is rendered once.
    /// </summary>
    public bool Solve(Guid userId, DateTimeOffset now)
    {
        if (SolvedAt is not null)
        {
            return false;
        }

        if (ClosedAt is null)
        {
            ClosedAt = now;
            CloseReason = EpisodeCloseReason.Solved;
        }

        SolvedByUserId = userId;
        SolvedAt = now;
        Unacknowledge();
        return true;
    }
}
