namespace Bugler.Access;

public sealed class AccessOptions
{
    public const string SectionName = "Access";

    /// <summary>
    /// How long an issued Session is trusted before its User is read back from the database.
    /// Zero — the default — revalidates on every request, so deactivating a User ends their Session
    /// at once. That costs one indexed lookup per authenticated request, next to nothing against the
    /// telemetry queries the Session is guarding (and non-admins already query Access once per request
    /// for their Visibility Scope). Raise it only to trade that lookup for a window in which a
    /// deactivated User keeps reading.
    /// </summary>
    public int SessionRevalidationSeconds { get; set; }
}
