using Bugler.SharedKernel;

namespace Bugler.Registry.Contracts;

/// <summary>Read-only view of the Catalog for other contexts: which Services exist and where they belong.</summary>
public interface ICatalogReader
{
    Task<IReadOnlyList<CatalogService>> GetServicesAsync(CancellationToken cancellationToken);
}

/// <summary>One registered Service, with the facets a Source Filter addresses it by.</summary>
public sealed record CatalogService(
    ServiceId Id,
    ApplicationId ApplicationId,
    string ApplicationName,
    string Namespace,
    string Environment,
    string Name);
