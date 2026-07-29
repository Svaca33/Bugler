namespace Bugler.Alerting.DetectEpisodes;

/// <summary>
/// The detection high-water mark over `telemetry.log_records.id` — single row. Seeded to MAX(id)
/// on first run so alerting watches from install time forward instead of replaying history.
/// </summary>
public sealed class PollCursor
{
    public const int SingletonId = 1;

    public required int Id { get; init; }
    public long LastLogId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
