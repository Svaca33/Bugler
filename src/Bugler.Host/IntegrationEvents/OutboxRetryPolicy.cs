namespace Bugler.Host.IntegrationEvents;

/// <summary>
/// How long a failed delivery waits before the next try, and when trying stops.
/// Backoff doubles from a second so a database blip resolves in seconds, while a
/// lasting fault does not spin (ADR 0008).
/// </summary>
internal static class OutboxRetryPolicy
{
    /// <summary>Failures after which a message is parked instead of retried.</summary>
    public const int MaxAttempts = 10;

    private static readonly TimeSpan FirstDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromMinutes(5);

    /// <summary>The wait after <paramref name="failures"/> failed deliveries (1 = the first failure).</summary>
    public static TimeSpan DelayAfter(int failures)
    {
        if (failures < 1)
        {
            return FirstDelay;
        }

        // Beyond ~2^30 seconds the shift overflows and the cap applies anyway.
        var doublings = Math.Min(failures - 1, 30);
        var delay = FirstDelay * Math.Pow(2, doublings);
        return delay < MaxDelay ? delay : MaxDelay;
    }

    /// <summary>Whether a message with this many failures is given up on.</summary>
    public static bool ShouldPark(int failures) => failures >= MaxAttempts;
}
