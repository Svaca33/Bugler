using Bugler.Registry.Contracts;
using Bugler.SharedKernel;

namespace Bugler.Alerting.Settings;

/// <summary>
/// How far one Episode reaches (see CONTEXT.md: Episode Scope): the Application, plus whichever
/// facets of the sender must match before two Matches of one kind share an Episode. Environment
/// stands by default and the other two do not — so the same trouble in two deployments of one
/// Application meets in one Episode while production and staging never do (ADR 0034).
///
/// The Logs Watch's alone. Under the Health Check Watch a Service is <em>what is being watched</em>
/// rather than where the trouble happened, so there is nothing to fold and the key is the Service.
/// </summary>
public sealed record EpisodeScope(bool ByNamespace, bool ByEnvironment, bool ByServiceName)
{
    /// <summary>What an Application scopes by until an Admin says otherwise.</summary>
    public static readonly EpisodeScope Default = new(false, true, false);

    /// <summary>What the column holds: three facets of 200 plus the Application's id and the labels.</summary>
    public const int MaxKeyLength = 700;

    /// <summary>
    /// The Scope key an Episode is bound by: one canonical string, facets in a fixed order and
    /// only those the Scope carries. One column rather than four nullable ones, because four
    /// would make every query read all of them to ask one question — and it is derived, written
    /// once when the Episode opens and never recomputed: a Scope change mutes rather than rewrites.
    /// </summary>
    public string KeyOf(CatalogService service)
    {
        var key = $"app={service.ApplicationId.Value}";
        if (ByEnvironment)
        {
            key += $"|env={service.Environment}";
        }

        if (ByNamespace)
        {
            key += $"|ns={service.Namespace}";
        }

        if (ByServiceName)
        {
            key += $"|name={service.Name}";
        }

        return key;
    }

    /// <summary>A Health Check Episode's key: its own Service, whatever the Application's Scope says.</summary>
    public static string KeyOfService(ServiceId serviceId) => $"service={serviceId.Value}";
}
