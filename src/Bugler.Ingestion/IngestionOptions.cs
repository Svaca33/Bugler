namespace Bugler.Ingestion;

public sealed class IngestionOptions
{
    public const string SectionName = "Ingestion";

    /// <summary>Maximum Signals held in memory before new Export Requests are rejected with 503.</summary>
    public int BufferCapacity { get; set; } = 50_000;

    /// <summary>Maximum Signals written to PostgreSQL in one COPY Batch.</summary>
    public int MaxBatchSize { get; set; } = 5_000;

    /// <summary>How often expired telemetry is purged.</summary>
    public int PurgeIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// How long a Declared Version a Service has left may keep arriving before the next sighting of
    /// it counts as a Release again (ADR 0016). Generous on purpose: too short invents a Release out
    /// of a slow rolling deploy, while too long only passes over a rollback made within it — and a
    /// marker where nothing was deployed is worse than a missing one for something done by hand
    /// minutes ago.
    /// </summary>
    public TimeSpan ReleaseStragglerWindow { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// How many Signals one purge statement removes before committing and starting the next.
    /// Bounds the transaction, not the run: a run keeps issuing statements until nothing expired
    /// is left.
    /// </summary>
    public int PurgeBatchSize { get; set; } = 50_000;
}
