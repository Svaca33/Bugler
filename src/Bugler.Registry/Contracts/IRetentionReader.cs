using Bugler.SharedKernel;

namespace Bugler.Registry.Contracts;

/// <summary>Tells Ingestion how long each Service's telemetry may live (override or server default).</summary>
public interface IRetentionReader
{
    Task<IReadOnlyList<ServiceRetention>> GetEffectiveRetentionsAsync(CancellationToken cancellationToken);
}

/// <summary>
/// One Service's Effective Retention: two clocks, because logs and traces are not read on the
/// same one. Both are already resolved — the Service's override where it has one, the server
/// default where it has none — so a purge never has to know which of the two it got.
/// </summary>
public sealed record ServiceRetention(ServiceId ServiceId, int LogDays, int TraceDays);
