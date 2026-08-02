using Microsoft.Extensions.Logging;
using Npgsql;

namespace Bugler.Ingestion.Storage;

/// <summary>
/// Persists a Batch with one binary COPY, and puts a refused one through a Salvage instead of
/// letting one Signal cost the rest (ADR 0020).
///
/// The two failures are not the same failure. PostgreSQL answering "no" means the Batch reached it
/// and something inside the Batch was unacceptable — so some of it is probably fine, and it is
/// worth finding out which. Anything else means PostgreSQL was not reached at all, where every
/// Signal in the Batch is equally doomed and halving would only spend the budget on connections
/// that cannot be opened. Only the first is salvaged; the second is the bounded loss ADR 0003
/// accepts, logged and left.
///
/// Salvaging halves rather than retrying row by row. One bad Signal in a Batch of five thousand
/// costs about twenty-six COPY attempts this way against five thousand, which matters because a
/// sender that emits one bad Signal emits it in every Batch, forever. What halving cannot bound is
/// the case where every Signal fails, so <see cref="MaxAttempts"/> does: past it the rest of the
/// Batch is given up and the pathological case falls back to being ADR 0003's bounded loss, which
/// is where it belongs.
///
/// Each attempt takes its own connection. The one that just carried a refused COPY is not worth
/// reusing to find out what else the Batch is hiding.
/// </summary>
/// <param name="signalName">Plural, for the log — the reader wants "spans", not a type name.</param>
internal sealed class BatchImporter<TRow>(
    NpgsqlDataSource dataSource,
    string copyCommand,
    Func<NpgsqlBinaryImporter, TRow, CancellationToken, Task> writeRow,
    string signalName,
    ILogger logger)
    where TRow : ITelemetryRow
{
    /// <summary>
    /// COPY attempts one Salvage may spend. Enough to isolate a handful of bad Signals in a full
    /// Batch with room to spare; past that it is not one sender logging a stray byte but something
    /// systemic, and a Batch nothing will accept is cheaper given up than dissected.
    /// </summary>
    private const int MaxAttempts = 128;

    public async Task ImportAsync(List<TRow> batch, CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return;
        }

        try
        {
            await CopyAsync(batch, 0, batch.Count, cancellationToken);
            return;
        }
        catch (PostgresException refusal)
        {
            logger.LogWarning(
                refusal,
                "PostgreSQL refused a batch of {Count} {Signal} ({SqlState}); salvaging what it will hold",
                batch.Count, signalName, refusal.SqlState);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                exception, "Failed to persist a batch of {Count} {Signal}; batch lost", batch.Count, signalName);
            return;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            // A refusal that is really the shutdown cancelling the COPY. Nothing to dissect.
            return;
        }

        var tally = new SalvageTally();
        await SalvageAsync(batch, 0, batch.Count, tally, cancellationToken);

        logger.LogWarning(
            "Salvaged {Stored} of {Count} {Signal} in {Attempts} attempts; {Refused} refused one by one, {GivenUp} given up",
            tally.Stored, batch.Count, signalName, tally.Attempts, tally.Refused, tally.GivenUp);
    }

    private async Task SalvageAsync(
        List<TRow> batch, int offset, int count, SalvageTally tally, CancellationToken cancellationToken)
    {
        if (tally.Abandoned || tally.Attempts >= MaxAttempts)
        {
            tally.GivenUp += count;
            return;
        }

        tally.Attempts++;

        try
        {
            await CopyAsync(batch, offset, count, cancellationToken);
            tally.Stored += count;
        }
        catch (PostgresException refusal) when (count == 1)
        {
            tally.Refused++;
            logger.LogError(
                refusal,
                "PostgreSQL refused one of the {Signal} sent by service {ServiceId} ({SqlState}); it is dropped and "
                + "the rest of its batch stands. The Signal itself is not logged: it is the sender's, and unbounded",
                signalName, batch[offset].ServiceId, refusal.SqlState);
        }
        catch (PostgresException)
        {
            var half = count / 2;
            await SalvageAsync(batch, offset, half, tally, cancellationToken);
            await SalvageAsync(batch, offset + half, count - half, tally, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // PostgreSQL stopped answering midway. Whatever is left of the Batch is beyond saving,
            // and every further attempt would only be another connection that cannot be opened.
            tally.Abandoned = true;
            tally.GivenUp += count;
            logger.LogError(
                exception,
                "Could not reach PostgreSQL while salvaging a {Signal} batch; the rest of it is abandoned",
                signalName);
        }
    }

    private async Task CopyAsync(List<TRow> batch, int offset, int count, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var importer = await connection.BeginBinaryImportAsync(copyCommand, cancellationToken);

        for (var index = offset; index < offset + count; index++)
        {
            await importer.StartRowAsync(cancellationToken);
            await writeRow(importer, batch[index], cancellationToken);
        }

        await importer.CompleteAsync(cancellationToken);
    }

    /// <summary>What one Salvage spent and what it came back with — the whole of its log line.</summary>
    private sealed class SalvageTally
    {
        public int Attempts;
        public int Stored;
        public int Refused;
        public int GivenUp;
        public bool Abandoned;
    }
}
