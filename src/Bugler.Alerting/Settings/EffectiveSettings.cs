using Bugler.Registry.Contracts;
using Bugler.SharedKernel;

namespace Bugler.Alerting.Settings;

/// <summary>
/// The settings detection actually runs under: every registered Service resolved through
/// `service override ?? application setting ?? default`, snapshotted at one instant. Rebuilt
/// each run, because detection always evaluates the current effective Sensitivity.
/// </summary>
public sealed class EffectiveSettings
{
    private readonly Dictionary<ServiceId, ResolvedService> _services;

    private EffectiveSettings(Dictionary<ServiceId, ResolvedService> services) => _services = services;

    public static EffectiveSettings Build(
        IReadOnlyList<CatalogService> catalog,
        IReadOnlyList<ApplicationAlertingSettings> applicationSettings,
        IReadOnlyList<ServiceAlertingSettings> serviceSettings)
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
                    ?? AlertingDefaults.QuietWindowMinutes);
        }

        return new EffectiveSettings(services);
    }

    /// <summary>Unknown Services do not alert: telemetry without a registration is an orphan, not an outage.</summary>
    public Sensitivity SensitivityOf(ServiceId serviceId) =>
        _services.GetValueOrDefault(serviceId)?.Sensitivity ?? Sensitivity.Off;

    public TimeSpan QuietWindowOf(ServiceId serviceId) => TimeSpan.FromMinutes(
        _services.GetValueOrDefault(serviceId)?.QuietWindowMinutes ?? AlertingDefaults.QuietWindowMinutes);

    public ApplicationId? ApplicationOf(ServiceId serviceId) =>
        _services.GetValueOrDefault(serviceId)?.ApplicationId;

    /// <summary>
    /// The one severity floor the telemetry poll filters by: the lowest floor any watched Service
    /// wants. Null when every Service is Off, which turns the poll into a cursor advance only.
    /// </summary>
    public short? GlobalSeverityFloor
    {
        get
        {
            short? floor = null;
            foreach (var service in _services.Values)
            {
                var serviceFloor = service.Sensitivity.SeverityFloor();
                if (serviceFloor is not null && (floor is null || serviceFloor < floor))
                {
                    floor = serviceFloor;
                }
            }

            return floor;
        }
    }

    /// <summary>The Services of one Application whose effective Sensitivity is Off right now — the set a settings change must close silently.</summary>
    public IReadOnlyList<ServiceId> ServicesEffectivelyOff(ApplicationId applicationId) =>
        _services
            .Where(s => s.Value.ApplicationId == applicationId && s.Value.Sensitivity == Sensitivity.Off)
            .Select(s => s.Key)
            .ToList();

    private sealed record ResolvedService(
        ApplicationId ApplicationId, Sensitivity Sensitivity, int QuietWindowMinutes);
}
