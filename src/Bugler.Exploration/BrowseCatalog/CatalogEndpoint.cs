using Bugler.Access.Contracts;
using Bugler.Registry.Contracts;

namespace Bugler.Exploration.BrowseCatalog;

public sealed record CatalogServiceDto(Guid Id, string Namespace, string Environment, string Name);

public sealed record CatalogApplicationDto(Guid Id, string Name, IReadOnlyList<CatalogServiceDto> Services);

public sealed record CatalogResponse(IReadOnlyList<CatalogApplicationDto> Applications);

/// <summary>
/// Names and structure of the caller's visible telemetry sources. The facets are what
/// a Source Filter addresses, so this is also where the filter's options come from — which is
/// exactly why it answers through the Focus by default: an Application a reader is not attending
/// to must not turn up in a dropdown, and filtering here is what empties every dropdown at once
/// rather than one screen at a time (Access ADR 0004).
///
/// <c>scope=all</c> asks for the whole Visibility Scope instead. Two screens need it and both are
/// settings rather than reading: the card where a Focus is chosen, which would otherwise be unable
/// to offer what is not already chosen, and the People tab, whose grant columns are the Admin's
/// management of somebody else's reading and not their own view.
/// </summary>
internal static class CatalogEndpoint
{
    public static async Task<CatalogResponse> Handle(
        string? scope,
        IReadVisibility visibility,
        IReadApplicationFocus focus,
        ICatalogReader catalog,
        CancellationToken cancellationToken)
    {
        var shown = string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase)
            ? await visibility.GetVisibleApplicationsAsync(cancellationToken)
            : await focus.GetFocusedApplicationsAsync(cancellationToken);

        return Compose(shown, await catalog.GetServicesAsync(cancellationToken));
    }

    /// <summary>
    /// The shaping alone, with the deciding already done. Separate because the machine door serves
    /// this same answer and must reach it without ever naming a Focus: a Machine Delegation lends
    /// the Visibility Scope, and an architecture test holds every Mcp folder to that.
    /// </summary>
    public static CatalogResponse Compose(
        IReadOnlyCollection<ApplicationId>? shown, IReadOnlyList<CatalogService> services)
    {
        var applications = services
            .Where(s => shown is null || shown.Contains(s.ApplicationId))
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
