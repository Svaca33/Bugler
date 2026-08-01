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
}
