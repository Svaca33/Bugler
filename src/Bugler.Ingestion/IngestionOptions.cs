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
    /// How many Signals one purge statement removes before committing and starting the next.
    /// Bounds the transaction, not the run: a run keeps issuing statements until nothing expired
    /// is left.
    /// </summary>
    public int PurgeBatchSize { get; set; } = 50_000;
}
