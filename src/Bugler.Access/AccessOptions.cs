namespace Bugler.Access;

public sealed class AccessOptions
{
    public const string SectionName = "Access";

    /// <summary>
    /// The section holding facts about the server rather than about Access. Bound onto these same
    /// options after <see cref="SectionName"/>, because <see cref="PublicBaseUrl"/> is one of them
    /// and Alerting needs the very same value (ADR 0011).
    /// </summary>
    public const string ServerSectionName = "Server";

    /// <summary>
    /// Public origin of this Bugler (e.g. https://bugler.example.com). The reset link in a mail is
    /// built from it; empty means Bugler does not know where it is reachable and offers no reset.
    /// It is also the server's only statement about whether TLS stands in front of it, and so
    /// decides whether the Session cookie is minted with <c>Secure</c> (ADR 0019) — which makes an
    /// http address here, or a wrong one, a security setting rather than a cosmetic one.
    /// </summary>
    public string PublicBaseUrl { get; set; } = "";

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
