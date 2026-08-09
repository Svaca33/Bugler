namespace Bugler.Alerting.Readings;

/// <summary>
/// The machine's reading of an Episode's opening evidence (see CONTEXT.md: Reading), one row per
/// Episode and only for Episodes whose Application consented while AI was configured. Carries the
/// text in every language Bugler speaks, so each channel and viewer takes its own — and, like the
/// Episode's own snapshot, it outlives the purge of the evidence it was read from.
/// </summary>
public sealed class Reading
{
    public const int MaxAttempts = 4;

    public required Guid EpisodeId { get; init; }

    /// <summary>The moment the Episode opened and owed this row — what the Alert's patience is measured from.</summary>
    public required DateTimeOffset RequestedAt { get; init; }

    public string? English { get; set; }
    public string? Czech { get; set; }

    /// <summary>Which model wrote it — the visible "machine-made" mark needs a name to show.</summary>
    public string? Model { get; set; }

    public DateTimeOffset? WrittenAt { get; set; }

    /// <summary>Terminal: attempts exhausted, consent withdrawn, or AI unconfigured. The Alert stops waiting.</summary>
    public DateTimeOffset? FailedAt { get; set; }

    public int Attempts { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public string? LastError { get; set; }

    /// <summary>Still owed: neither written nor given up on — the state the Alert holds the door for.</summary>
    public bool IsPending => WrittenAt is null && FailedAt is null;
}
