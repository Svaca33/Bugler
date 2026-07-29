namespace Bugler.Alerting.DeliverMessages;

/// <summary>The backoff a failed Delivery waits out: 1, 2, 4, 8 minutes, then 15 flat.</summary>
public static class RetrySchedule
{
    public static TimeSpan NextDelay(int attempts) =>
        TimeSpan.FromMinutes(Math.Min(Math.Pow(2, Math.Max(attempts, 1) - 1), 15));
}
