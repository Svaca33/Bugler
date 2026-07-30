using Npgsql;

namespace Bugler.Host;

/// <summary>
/// What `/health` answers. A probe that only proves the process is listening reports OK while
/// every query behind it fails — which is worse than no probe at all, because it actively calms
/// whoever is watching. So the check reaches PostgreSQL.
///
/// Not on every request, though: `/health` is anonymous and answers on the OTLP surfaces too,
/// which face the internet. One answer is held for a few seconds and handed to everyone who asks
/// in the meantime, so a probe cannot be turned into a way of asking the database anything.
/// </summary>
internal sealed class HealthProbe(NpgsqlDataSource dataSource, ILogger<HealthProbe> logger)
{
    /// <summary>How long an answer stands before it is asked again.</summary>
    private static readonly TimeSpan Freshness = TimeSpan.FromSeconds(5);

    /// <summary>
    /// How long the database has to answer. Short: a probe that hangs tells its reader nothing,
    /// and a database this slow to say "1" is not one the app is serving requests from either.
    /// </summary>
    private const int BudgetSeconds = 3;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile Answer _last = new(Healthy: false, At: DateTimeOffset.MinValue);

    public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken)
    {
        if (IsFresh(_last))
        {
            return _last.Healthy;
        }

        // Exactly one caller goes to the database; everyone arriving while it is out takes the
        // answer standing, rather than queueing behind it with the same question.
        if (!await _gate.WaitAsync(TimeSpan.Zero, cancellationToken))
        {
            return _last.Healthy;
        }

        try
        {
            // Someone may have refreshed it between the check above and the gate.
            if (IsFresh(_last))
            {
                return _last.Healthy;
            }

            var healthy = await AskAsync(cancellationToken);
            _last = new Answer(healthy, DateTimeOffset.UtcNow);
            return healthy;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static bool IsFresh(Answer answer) => DateTimeOffset.UtcNow - answer.At < Freshness;

    private async Task<bool> AskAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var command = dataSource.CreateCommand("SELECT 1");
            command.CommandTimeout = BudgetSeconds;
            await command.ExecuteScalarAsync(cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Logged rather than surfaced: the endpoint says only whether Bugler is serving, and
            // it says that to anyone who asks. What is wrong belongs where the operator reads.
            logger.LogWarning(exception, "Health probe could not reach the database");
            return false;
        }
    }

    private sealed record Answer(bool Healthy, DateTimeOffset At);
}
