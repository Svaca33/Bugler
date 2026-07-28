using Bugler.SharedKernel;

namespace Bugler.Registry.Contracts;

/// <summary>Tells Ingestion how long each Service's telemetry may live (override or server default).</summary>
public interface IRetentionReader
{
    Task<IReadOnlyList<ServiceRetention>> GetEffectiveRetentionsAsync(CancellationToken cancellationToken);
}

public sealed record ServiceRetention(ServiceId ServiceId, int Days);
