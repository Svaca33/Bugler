using Bugler.Access.Contracts;
using Bugler.Registry.Contracts;
using Bugler.SharedKernel;

namespace Bugler.Exploration.Scoping;

/// <summary>
/// Turns the caller's Visibility Scope plus optional request filters into the set of
/// instance ids a query may touch. Every Exploration query goes through this — the
/// scope cannot be widened by request parameters.
/// </summary>
public sealed class ScopeResolver(IReadVisibility visibility, ICatalogReader catalog)
{
    /// <returns>
    /// Instance ids the query must be limited to, or null when no instance restriction
    /// applies (unrestricted caller, no filters). An empty array means "show nothing".
    /// </returns>
    public async Task<Guid[]?> ResolveInstanceIdsAsync(
        ApplicationId? applicationFilter,
        InstanceId? instanceFilter,
        CancellationToken cancellationToken)
    {
        var visibleApplications = await visibility.GetVisibleApplicationsAsync(cancellationToken);

        if (visibleApplications is null && applicationFilter is null)
        {
            // Unrestricted caller: an instance filter passes through as-is.
            return instanceFilter is { } instance ? [instance.Value] : null;
        }

        var instances = await catalog.GetInstancesAsync(cancellationToken);
        var allowed = instances
            .Where(i => visibleApplications is null || visibleApplications.Contains(i.ApplicationId))
            .Where(i => applicationFilter is null || i.ApplicationId == applicationFilter)
            .Where(i => instanceFilter is null || i.Id == instanceFilter)
            .Select(i => i.Id.Value)
            .ToArray();

        return allowed;
    }
}
