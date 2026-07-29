namespace Bugler.Alerting.DetectEpisodes;

/// <summary>
/// A matched log id inside the overlap window, kept so the overlap re-read (ADR 0010) never
/// counts the same Log Record twice. Pruned once the high-water mark moves past the overlap.
/// </summary>
public sealed class SeenLogId
{
    public required long LogId { get; init; }
}
