using Bugler.Registry.Contracts;
using Bugler.SharedKernel;

namespace Bugler.Alerting.Settings;

/// <summary>
/// The settings detection actually runs under: every registered Service resolved through
/// `service override ?? application setting ?? default`, with the Quiet Window resolving one
/// tier deeper still (`kind of trouble ?? that`, ADR 0004) — all snapshotted at one instant.
/// Rebuilt each run, because detection always evaluates the current effective Sensitivity.
/// Always built whole: one shared path, no second opinion.
/// </summary>
public sealed class EffectiveSettings
{
    private readonly Dictionary<ServiceId, ResolvedService> _services;
    private readonly Dictionary<(ServiceId, string), int> _fingerprintWindows;

    private EffectiveSettings(
        Dictionary<ServiceId, ResolvedService> services,
        Dictionary<(ServiceId, string), int> fingerprintWindows)
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
            services[service.Id] = new ResolvedService(
                service.ApplicationId,
                overrides?.Sensitivity ?? application?.Sensitivity ?? AlertingDefaults.Sensitivity,
                overrides?.QuietWindowMinutes ?? application?.QuietWindowMinutes
                    ?? AlertingDefaults.QuietWindowMinutes,
                // The one setting with no tier above it: an address cannot be inherited.
                overrides?.HealthCheckUrl);
        }

        return new EffectiveSettings(
            services,
            fingerprintWindows.ToDictionary(
                w => (w.ServiceId, w.Fingerprint), w => w.QuietWindowMinutes));
    }

    /// <summary>Unknown Services do not alert: telemetry without a registration is an orphan, not an outage.</summary>
    public Sensitivity SensitivityOf(ServiceId serviceId) =>
        _services.GetValueOrDefault(serviceId)?.Sensitivity ?? Sensitivity.Off;

    /// <summary>
    /// How long this kind of trouble in this Service must stay silent. The Fingerprint's own
    /// window wins where one is set; otherwise the Service's resolved value applies.
    /// </summary>
    public TimeSpan QuietWindowOf(ServiceId serviceId, string fingerprint) =>
        TimeSpan.FromMinutes(QuietWindowMinutesOf(serviceId, fingerprint));

    public int QuietWindowMinutesOf(ServiceId serviceId, string fingerprint) =>
        _fingerprintWindows.TryGetValue((serviceId, fingerprint), out var own)
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
        string? HealthCheckUrl);
}
