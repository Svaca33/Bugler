namespace Bugler.Access.Contracts;

/// <summary>
/// The names an endpoint uses to say what it needs done, rather than who the caller is
/// (ADR 0015). Access decides what each one currently means — today every capability resolves
/// to the Admin role, so the seam costs nothing until roles and permissions arrive.
/// </summary>
public static class Capabilities
{
    /// <summary>Changing how the unattended watch behaves: Sensitivity, Quiet Windows, the Chat Webhook.</summary>
    public const string ConfigureAlerting = "ConfigureAlerting";

    /// <summary>Reading what the stored telemetry costs: each Service's Footprint and Ingest Rate.</summary>
    public const string InspectStorage = "InspectStorage";

    /// <summary>
    /// Seeing every Machine Delegation issued on this server and revoking any of them — what answering for
    /// the machine door requires of whoever opened it (ADR 0029).
    /// </summary>
    public const string InspectMachineDelegations = "InspectMachineDelegations";

    /// <summary>Deciding whether this server opens a machine door at all, and where it answers.</summary>
    public const string ConfigureMcp = "ConfigureMcp";

    /// <summary>
    /// Removing a kind of trouble from the record for good — every Episode of it, Journal and all
    /// (Alerting CONTEXT.md: Deletion). Destroying who acknowledged and who solved is no triage
    /// gesture, so it is asked for by its own name rather than under ConfigureAlerting.
    /// </summary>
    public const string DeleteKindsOfTrouble = "DeleteKindsOfTrouble";
}
