using Bugler.Alerting.Episodes;
using Bugler.Registry.Contracts;
using Bugler.SharedKernel;

namespace Bugler.Alerting.Settings;

/// <summary>
/// The settings detection actually runs under: every registered Service resolved through
/// `service override ?? application setting ?? default`, with the Quiet Window resolving one
/// tier deeper still (`kind of trouble ?? that`, ADR 0004) — all snapshotted at one instant.
/// Rebuilt each run, because detection always evaluates the current effective Sensitivity.
/// Always built whole: one shared path, no second opinion.
///
/// The Fingerprint Rule and the Episode Scope have no Service tier under them (ADR 0033, 0034):
/// an Episode reaches across Services, so the two ends must agree on what "the same trouble" is
/// and how far it reaches. Both resolve per Application and are read here as the Service's own
/// answer, because that is where detection asks the question.
/// </summary>
public sealed class EffectiveSettings
{
    private readonly Dictionary<ServiceId, ResolvedService> _services;
    private readonly Dictionary<(string ScopeKey, Watch Watch, string Fingerprint), int>
        _fingerprintWindows;

    private EffectiveSettings(
        Dictionary<ServiceId, ResolvedService> services,
        Dictionary<(string, Watch, string), int> fingerprintWindows)
    {
        _services = services;
        _fingerprintWindows = fingerprintWindows;
    }

    public static EffectiveSettings Build(
        IReadOnlyList<CatalogService> catalog,
        IReadOnlyList<ApplicationAlertingSettings> applicationSettings,
        IReadOnlyList<ServiceAlertingSettings> serviceSettings,
        IReadOnlyList<FingerprintQuietWindow> fingerprintWindows)
    {
        var byApplication = applicationSettings.ToDictionary(s => s.ApplicationId);
        var byService = serviceSettings.ToDictionary(s => s.ServiceId);

        var services = new Dictionary<ServiceId, ResolvedService>(catalog.Count);
        foreach (var service in catalog)
        {
            var application = byApplication.GetValueOrDefault(service.ApplicationId);
            var overrides = byService.GetValueOrDefault(service.Id);
            var scope = ScopeOf(application);
            services[service.Id] = new ResolvedService(
                service.ApplicationId,
                overrides?.Sensitivity ?? application?.Sensitivity ?? AlertingDefaults.Sensitivity,
                overrides?.QuietWindowMinutes ?? application?.QuietWindowMinutes
                    ?? AlertingDefaults.QuietWindowMinutes,
                // The one setting with no tier above it: an address cannot be inherited.
                overrides?.HealthCheckUrl,
                scope.KeyOf(service),
                application?.FingerprintRule ?? AlertingDefaults.FingerprintRule,
                string.IsNullOrWhiteSpace(application?.FingerprintAttributeKey)
                    ? null
                    : application.FingerprintAttributeKey.Trim());
        }

        return new EffectiveSettings(
            services,
            fingerprintWindows.ToDictionary(
                w => (w.ScopeKey, w.Watch, w.Fingerprint), w => w.QuietWindowMinutes));
    }

    /// <summary>An Application with no row, or nulls in it, scopes the way ADR 0034 says by default.</summary>
    public static EpisodeScope ScopeOf(ApplicationAlertingSettings? settings) =>
        settings is null
            ? EpisodeScope.Default
            : new EpisodeScope(
                settings.ScopeByNamespace ?? EpisodeScope.Default.ByNamespace,
                settings.ScopeByEnvironment ?? EpisodeScope.Default.ByEnvironment,
                settings.ScopeByServiceName ?? EpisodeScope.Default.ByServiceName);

    /// <summary>Unknown Services do not alert: telemetry without a registration is an orphan, not an outage.</summary>
    public Sensitivity SensitivityOf(ServiceId serviceId) =>
        _services.GetValueOrDefault(serviceId)?.Sensitivity ?? Sensitivity.Off;

    /// <summary>
    /// How far an Episode this Service's Log Records fall into reaches (see CONTEXT.md: Episode
    /// Scope). Null for a Service nothing knows — orphan telemetry is bound by nothing.
    /// </summary>
    public string? ScopeKeyOf(ServiceId serviceId) =>
        _services.GetValueOrDefault(serviceId)?.ScopeKey;

    /// <summary>How this Service's Application distills its Fingerprints (see CONTEXT.md: Fingerprint Rule).</summary>
    public FingerprintRule FingerprintRuleOf(ServiceId serviceId) =>
        _services.GetValueOrDefault(serviceId)?.FingerprintRule ?? AlertingDefaults.FingerprintRule;

    /// <summary>The attribute that outranks the Rule for this Service's Application; null where none is named.</summary>
    public string? FingerprintAttributeKeyOf(ServiceId serviceId) =>
        _services.GetValueOrDefault(serviceId)?.FingerprintAttributeKey;

    /// <summary>Every attribute key any Application names — the extra columns one poll page has to read.</summary>
    public IReadOnlyList<string> NamedAttributeKeys() =>
        _services.Values
            .Select(s => s.FingerprintAttributeKey)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// How long this kind of trouble in this Episode Scope must stay silent — the kind named the
    /// way an Episode names it, Watch and all. The Fingerprint's own window wins where one is set;
    /// otherwise the Service's resolved value applies — Sensitivity and the Quiet Window stay per
    /// Service even where the Episode does not, and that is correct: an Episode may be fed by
    /// Services configured differently.
    /// </summary>
    public TimeSpan QuietWindowOf(
        ServiceId serviceId, string scopeKey, Watch watch, string fingerprint) =>
        TimeSpan.FromMinutes(QuietWindowMinutesOf(serviceId, scopeKey, watch, fingerprint));

    public int QuietWindowMinutesOf(
        ServiceId serviceId, string scopeKey, Watch watch, string fingerprint) =>
        _fingerprintWindows.TryGetValue((scopeKey, watch, fingerprint), out var own)
            ? own
            : _services.GetValueOrDefault(serviceId)?.QuietWindowMinutes
                ?? AlertingDefaults.QuietWindowMinutes;

    /// <summary>What a Service's Episodes fall back to — the value a Fingerprint override replaces.</summary>
    public int InheritedQuietWindowMinutesOf(ServiceId serviceId) =>
        _services.GetValueOrDefault(serviceId)?.QuietWindowMinutes ?? AlertingDefaults.QuietWindowMinutes;

    public ApplicationId? ApplicationOf(ServiceId serviceId) =>
        _services.GetValueOrDefault(serviceId)?.ApplicationId;

    /// <summary>Where this Service answers whether it is alive; null means the Health Check Watch is off for it.</summary>
    public string? HealthCheckUrlOf(ServiceId serviceId) =>
        _services.GetValueOrDefault(serviceId)?.HealthCheckUrl;

    /// <summary>The Services of one Application whose effective Sensitivity is Off right now — the set a settings change must close silently.</summary>
    public IReadOnlyList<ServiceId> ServicesEffectivelyOff(ApplicationId applicationId) =>
        _services
            .Where(s => s.Value.ApplicationId == applicationId && s.Value.Sensitivity == Sensitivity.Off)
            .Select(s => s.Key)
            .ToList();

    /// <summary>The Services of one Application nobody is asking — the set a cleared address must close silently.</summary>
    public IReadOnlyList<ServiceId> ServicesWithoutHealthCheck(ApplicationId applicationId) =>
        _services
            .Where(s => s.Value.ApplicationId == applicationId && s.Value.HealthCheckUrl is null)
            .Select(s => s.Key)
            .ToList();

    /// <summary>Every Service the Health Check Watch has an address for — one sweep's whole worklist.</summary>
    public IReadOnlyList<WatchedService> WatchedByHealthCheck() =>
        _services
            .Where(s => s.Value.HealthCheckUrl is not null)
            .Select(s => new WatchedService(s.Key, s.Value.ApplicationId, s.Value.HealthCheckUrl!))
            .ToList();

    private sealed record ResolvedService(
        ApplicationId ApplicationId,
        Sensitivity Sensitivity,
        int QuietWindowMinutes,
        string? HealthCheckUrl,
        string ScopeKey,
        FingerprintRule FingerprintRule,
        string? FingerprintAttributeKey);
}
