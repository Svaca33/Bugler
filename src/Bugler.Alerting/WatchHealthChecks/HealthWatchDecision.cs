namespace Bugler.Alerting.WatchHealthChecks;

/// <summary>What one probe's verdict does to the Service's Episode.</summary>
public enum HealthAction
{
    /// <summary>Nothing to record: the Service answered, or it has not failed often enough yet.</summary>
    None,

    /// <summary>Open an Episode and owe its Alerts — the Service has been unanswerable long enough to say so.</summary>
    Open,

    /// <summary>Another match on the Episode already open: it stays open and its Quiet Window stays reset.</summary>
    Feed,
}

/// <summary>The Health Check Watch's whole rule, free of I/O.</summary>
public static class HealthWatchDecision
{
    /// <summary>
    /// How many probes must fail in a row before an Episode opens. Detection over Log Records
    /// needs no such count — a Log Record is evidence that already happened — but a failed probe
    /// is Bugler's own observation and can simply be wrong. Three at the loop's beat is under a
    /// minute of blindness, bought against one lost packet waking everybody.
    /// </summary>
    public const int FailuresBeforeOpening = 3;

    /// <summary>
    /// The count gates opening only. Once an Episode is open its Alert has gone out, so there is
    /// no longer a false alarm to guard against and every failure is a match on the spot.
    /// </summary>
    public static HealthAction Decide(bool alive, int consecutiveFailures, bool episodeOpen) =>
        alive ? HealthAction.None
        : episodeOpen ? HealthAction.Feed
        : consecutiveFailures >= FailuresBeforeOpening ? HealthAction.Open
        : HealthAction.None;
}
