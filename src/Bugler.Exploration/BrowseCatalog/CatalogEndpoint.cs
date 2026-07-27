using Bugler.Access.Contracts;
using Bugler.Registry.Contracts;

namespace Bugler.Exploration.BrowseCatalog;

public sealed record CatalogInstanceDto(Guid Id, string Name);

public sealed record CatalogApplicationDto(Guid Id, string Name, IReadOnlyList<CatalogInstanceDto> Instances);

public sealed record CatalogResponse(IReadOnlyList<CatalogApplicationDto> Applications);

/// <summary>Names and structure of the caller's visible telemetry sources, for filters and display.</summary>
internal static class CatalogEndpoint
{
    public static async Task<CatalogResponse> Handle(
        IReadVisibility visibility,
        ICatalogReader catalog,
        CancellationToken cancellationToken)
    {
        var visibleApplications = await visibility.GetVisibleApplicationsAsync(cancellationToken);
        var instances = await catalog.GetInstancesAsync(cancellationToken);

        var applications = instances
            .Where(i => visibleApplications is null || visibleApplications.Contains(i.ApplicationId))
            .GroupBy(i => (i.ApplicationId, i.ApplicationName))
            .OrderBy(g => g.Key.ApplicationName)
            .Select(g => new CatalogApplicationDto(
                g.Key.ApplicationId.Value,
                g.Key.ApplicationName,
                g.OrderBy(i => i.InstanceName)
                    .Select(i => new CatalogInstanceDto(i.Id.Value, i.InstanceName))
                    .ToList()))
            .ToList();

        return new CatalogResponse(applications);
    }
}
