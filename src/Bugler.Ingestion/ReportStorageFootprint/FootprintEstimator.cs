namespace Bugler.Ingestion.ReportStorageFootprint;

/// <summary>
/// Splits a table's real size among the Services holding rows in it. The split is an estimate
/// and the report says so: each Service's share is its row count weighted by the average row
/// width it showed over the measured window — so a Service sending few but heavy Signals is
/// not hidden behind a row-proportional average — and the shares are then scaled so they sum
/// to what the table, its indexes and its TOAST actually occupy on disk.
/// </summary>
public static class FootprintEstimator
{
    /// <summary>One row's stored bytes measured by the window query (see <see cref="RowOverheadBytes"/>).</summary>
    public sealed record WindowSample(long Rows, long Bytes);

    /// <summary>
    /// What a heap row costs beyond its column values: the tuple header and the item pointer.
    /// Added per row when a window's bytes are read, so measured widths sit near the truth
    /// instead of systematically under it.
    /// </summary>
    public const int RowOverheadBytes = 28;

    /// <summary>
    /// Estimates the bytes each Service in <paramref name="rowsByService"/> occupies of
    /// <paramref name="tableBytes"/>. Services absent from <paramref name="windowByService"/>
    /// (nothing arrived in the window) weigh in at the average width of those that are present,
    /// or plain row proportion when the whole window was silent.
    /// </summary>
    public static IReadOnlyDictionary<Guid, long> Estimate(
        long tableBytes,
        IReadOnlyDictionary<Guid, long> rowsByService,
        IReadOnlyDictionary<Guid, WindowSample> windowByService)
    {
        var sampledRows = 0L;
        var sampledBytes = 0L;
        foreach (var sample in windowByService.Values)
        {
            sampledRows += sample.Rows;
            sampledBytes += sample.Bytes;
        }

        var fallbackWidth = sampledRows > 0 ? (double)sampledBytes / sampledRows : 1d;

        var weights = new Dictionary<Guid, double>(rowsByService.Count);
        var totalWeight = 0d;
        foreach (var (serviceId, rows) in rowsByService)
        {
            var width = windowByService.TryGetValue(serviceId, out var sample) && sample.Rows > 0
                ? (double)sample.Bytes / sample.Rows
                : fallbackWidth;
            var weight = rows * width;
            weights[serviceId] = weight;
            totalWeight += weight;
        }

        var estimates = new Dictionary<Guid, long>(rowsByService.Count);
        foreach (var (serviceId, weight) in weights)
        {
            estimates[serviceId] = totalWeight > 0
                ? (long)Math.Round(tableBytes * (weight / totalWeight))
                : 0;
        }

        return estimates;
    }
}
