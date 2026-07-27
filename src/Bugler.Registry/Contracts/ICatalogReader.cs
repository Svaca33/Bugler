using Bugler.SharedKernel;

namespace Bugler.Registry.Contracts;

/// <summary>Read-only view of the Catalog for other contexts: which Instances exist and where they belong.</summary>
public interface ICatalogReader
{
    Task<IReadOnlyList<CatalogInstance>> GetInstancesAsync(CancellationToken cancellationToken);
}

public sealed record CatalogInstance(
    InstanceId Id,
    ApplicationId ApplicationId,
    string InstanceName,
    string ApplicationName);
