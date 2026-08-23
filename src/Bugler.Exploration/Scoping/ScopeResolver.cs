using Bugler.Access.Contracts;
using Bugler.Registry.Contracts;

namespace Bugler.Exploration.Scoping;

/// <summary>
/// Turns the caller's Visibility Scope plus an optional Source Filter into the set of
/// service ids a query may touch. Every Exploration query goes through this — the
/// scope cannot be widened by request parameters.
///
/// Which of the caller's two sets it starts from depends on the Filter: a query that leaves the
/// Application facet open is answered through their Focus, and a query that names an Application
/// is answered from the Visibility Scope itself (Access ADR 0004). That is what makes a Focus a
/// lens rather than a lock — the UI never offers what a Focus hides, but a link that names it,
/// or a hand-written call, is still answered instead of refused.
/// </summary>
public sealed class ScopeResolver(
    IReadVisibility visibility, IReadApplicationFocus focus, ICatalogReader catalog)
{
    /// <returns>
    /// Service ids the query must be limited to, or null when no restriction applies
    /// (unrestricted caller, open filter). An empty array means "show nothing".
    /// </returns>
    public async Task<Guid[]?> ResolveServiceIdsAsync(
        SourceFilter filter,
        CancellationToken cancellationToken)
    {
        var applications = await ApplicationsFor(filter, cancellationToken);

        if (applications is null && filter.IsOpen)
        {
            return null;
        }

        return await NameServiceIdsAsync(filter, applications, cancellationToken);
    }

    /// <returns>
    /// The same set as <see cref="ResolveServiceIdsAsync"/>, but always named one by one: an
    /// unrestricted caller gets every registered Service rather than null. A query that reaches its
    /// rows through a `(service_id, …)` index needs a list to walk, and "no restriction" is not one.
    /// Naming them all is equivalent to leaving the scope open because telemetry never outlives its
    /// Service — deleting one erases what it sent (ADR 0007), so no rows sit outside this list.
    /// </returns>
    public async Task<Guid[]> ResolveEveryServiceIdAsync(
        SourceFilter filter,
        CancellationToken cancellationToken)
    {
        var applications = await ApplicationsFor(filter, cancellationToken);
        return await NameServiceIdsAsync(filter, applications, cancellationToken);
    }

    /// <summary>
    /// The Focus while the Application facet is open, the Visibility Scope once it names one. Both
    /// answers are already the caller's own — the Focus is resolved against the Scope before it
    /// gets here — so this chooses how narrow the answer is, never whether it is allowed.
    /// </summary>
    private async Task<IReadOnlyCollection<ApplicationId>?> ApplicationsFor(
        SourceFilter filter, CancellationToken cancellationToken) =>
        filter.Application is null
            ? await focus.GetFocusedApplicationsAsync(cancellationToken)
            : await visibility.GetVisibleApplicationsAsync(cancellationToken);

    private async Task<Guid[]> NameServiceIdsAsync(
        SourceFilter filter,
        IReadOnlyCollection<ApplicationId>? applications,
        CancellationToken cancellationToken)
    {
        var services = await catalog.GetServicesAsync(cancellationToken);
        return services
            .Where(s => applications is null || applications.Contains(s.ApplicationId))
            .Where(s => filter.Application is null || s.ApplicationId == filter.Application)
            .Where(s => filter.Namespace is null || s.Namespace == filter.Namespace)
            .Where(s => filter.Environment is null || s.Environment == filter.Environment)
            .Where(s => filter.Service is null || s.Name == filter.Service)
            .Select(s => s.Id.Value)
            .ToArray();
    }
}
