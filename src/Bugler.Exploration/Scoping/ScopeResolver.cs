using Bugler.Access.Contracts;
using Bugler.Registry.Contracts;

namespace Bugler.Exploration.Scoping;

/// <summary>
/// Turns the caller's Visibility Scope plus an optional Source Filter into the set of
/// service ids a query may touch. Every Exploration query goes through this — the
/// scope cannot be widened by request parameters.
/// </summary>
public sealed class ScopeResolver(IReadVisibility visibility, ICatalogReader catalog)
{
    /// <returns>
    /// Service ids the query must be limited to, or null when no restriction applies
    /// (unrestricted caller, open filter). An empty array means "show nothing".
    /// </returns>
    public async Task<Guid[]?> ResolveServiceIdsAsync(
        SourceFilter filter,
        CancellationToken cancellationToken)
    {
        var visibleApplications = await visibility.GetVisibleApplicationsAsync(cancellationToken);

        if (visibleApplications is null && filter.IsOpen)
        {
            return null;
        }

        var services = await catalog.GetServicesAsync(cancellationToken);
        return services
            .Where(s => visibleApplications is null || visibleApplications.Contains(s.ApplicationId))
            .Where(s => filter.Application is null || s.ApplicationId == filter.Application)
            .Where(s => filter.Namespace is null || s.Namespace == filter.Namespace)
            .Where(s => filter.Environment is null || s.Environment == filter.Environment)
            .Where(s => filter.Service is null || s.Name == filter.Service)
            .Select(s => s.Id.Value)
            .ToArray();
    }
}
