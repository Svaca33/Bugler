using Bugler.Access.Contracts;
using Bugler.Registry.Contracts;

namespace Bugler.Exploration.BrowseCatalog;

public sealed record CatalogServiceDto(Guid Id, string Namespace, string Environment, string Name);

public sealed record CatalogApplicationDto(Guid Id, string Name, IReadOnlyList<CatalogServiceDto> Services);

public sealed record CatalogResponse(IReadOnlyList<CatalogApplicationDto> Applications);

/// <summary>
/// Names and structure of the caller's visible telemetry sources. The facets are what
/// a Source Filter addresses, so this is also where the filter's options come from.
/// </summary>
internal static class CatalogEndpoint
{
    public static async Task<CatalogResponse> Handle(
        IReadVisibility visibility,
        ICatalogReader catalog,
        CancellationToken cancellationToken)
    {
        var visibleApplications = await visibility.GetVisibleApplicationsAsync(cancellationToken);
        var services = await catalog.GetServicesAsync(cancellationToken);

        var applications = services
            .Where(s => visibleApplications is null || visibleApplications.Contains(s.ApplicationId))
            .GroupBy(s => (s.ApplicationId, s.ApplicationName))
            .OrderBy(g => g.Key.ApplicationName)
            .Select(g => new CatalogApplicationDto(
                g.Key.ApplicationId.Value,
                g.Key.ApplicationName,
                g.OrderBy(s => s.Namespace).ThenBy(s => s.Environment).ThenBy(s => s.Name)
                    .Select(s => new CatalogServiceDto(s.Id.Value, s.Namespace, s.Environment, s.Name))
                    .ToList()))
            .ToList();

        return new CatalogResponse(applications);
    }
}
