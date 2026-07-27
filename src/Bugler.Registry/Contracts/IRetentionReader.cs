using Bugler.SharedKernel;

namespace Bugler.Registry.Contracts;

/// <summary>Tells Ingestion how long each Instance's telemetry may live (override or server default).</summary>
public interface IRetentionReader
{
    Task<IReadOnlyList<InstanceRetention>> GetEffectiveRetentionsAsync(CancellationToken cancellationToken);
}

public sealed record InstanceRetention(InstanceId InstanceId, int Days);
