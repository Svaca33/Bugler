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
    public EpisodeCloseReason? CloseReason { get; set; }
}
