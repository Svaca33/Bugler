using Bugler.SharedKernel;

namespace Bugler.Host.IntegrationEvents;

/// <summary>
/// The nudge from a publisher to the dispatcher. Signals collapse: several enqueues while the
/// dispatcher is busy wake it once, and it then drains everything that has come due.
/// </summary>
internal sealed class OutboxSignal : IOutboxSignal
{
    private readonly SemaphoreSlim _pending = new(0, 1);

    public void Nudge()
    {
        try
        {
            _pending.Release();
        }
        catch (SemaphoreFullException)
        {
            // Already nudged and not yet drained — one wake-up covers both.
        }
    }

    /// <summary>Waits for a nudge, giving up after <paramref name="timeout"/> so a lost signal only costs latency.</summary>
    public async Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            await _pending.WaitAsync(timeout, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Shutting down; the dispatcher checks the token itself.
        }
    }
}
