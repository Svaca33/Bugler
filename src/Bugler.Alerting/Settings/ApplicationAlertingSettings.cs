namespace Bugler.Alerting.Settings;

/// <summary>
/// An Application's detection settings. Absent row — and any null field — means the
/// <see cref="AlertingDefaults"/> apply, mirroring how retention falls back to the server default.
/// </summary>
public sealed class ApplicationAlertingSettings
{
    public required ApplicationId ApplicationId { get; init; }
    public Sensitivity? Sensitivity { get; set; }
    public int? QuietWindowMinutes { get; set; }

    /// <summary>How long this Application's Machine Claims lease for; null means the default applies.</summary>
    public int? ClaimLeaseHours { get; set; }

    /// <summary>The Chat Webhook (see CONTEXT.md) — a secret; endpoints expose only its host.</summary>
    public string? ChatWebhookUrl { get; set; }

    /// <summary>
    /// How this Application's Fingerprints are distilled (see CONTEXT.md: Fingerprint Rule); null
    /// means the default. Application-wide with no Service tier under it, unlike Sensitivity: an
    /// Episode reaches across Services and they must agree on what "the same trouble" means.
    /// </summary>
    public FingerprintRule? FingerprintRule { get; set; }

    /// <summary>The one attribute that outranks the Rule where a Match carries it; null means none is named.</summary>
    public string? FingerprintAttributeKey { get; set; }

    /// <summary>What the attribute key column holds — an OTel attribute name, not a sentence.</summary>
    public const int MaxAttributeKeyLength = 200;

    // The Episode Scope's facets (see CONTEXT.md: Episode Scope). Null means the default stands:
    // Environment alone, so two deployments of one Application meet while staging and production
    // never do. Stored as three flags rather than one enum because they are three independent
    // yes-or-no answers about the sender, and the key is built from whichever say yes.
    public bool? ScopeByNamespace { get; set; }
    public bool? ScopeByEnvironment { get; set; }
    public bool? ScopeByServiceName { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
