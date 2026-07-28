using Bugler.SharedKernel;

namespace Bugler.Exploration.Scoping;

/// <summary>
/// The Source Filter of a query: which Services it may touch, addressed by registered
/// facets rather than by picking one registration. Every facet left open means "all".
/// </summary>
public sealed record SourceFilter(
    ApplicationId? Application,
    string? Namespace,
    string? Environment,
    string? Service)
{
    public static readonly SourceFilter None = new(null, null, null, null);

    public bool IsOpen =>
        Application is null && Namespace is null && Environment is null && Service is null;

    /// <summary>Builds a filter from raw query values, treating blank strings as "not filtered".</summary>
    public static SourceFilter FromQuery(
        Guid? applicationId, string? serviceNamespace, string? environment, string? service) =>
        new(
            applicationId is { } id ? new ApplicationId(id) : null,
            Normalize(serviceNamespace),
            Normalize(environment),
            Normalize(service));

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
