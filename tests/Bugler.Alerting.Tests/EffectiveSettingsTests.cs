using Bugler.Alerting.Settings;
using Bugler.Registry.Contracts;
using Bugler.SharedKernel;

namespace Bugler.Alerting.Tests;

public class EffectiveSettingsTests
{
    private static readonly ApplicationId App = ApplicationId.New();
    private static readonly ServiceId Web = ServiceId.New();
    private static readonly ServiceId Worker = ServiceId.New();

    private const string Kind = "Payment declined: {Reason}";
    private const string OtherKind = "Warehouse timed out";

    private static CatalogService Registered(ServiceId id, ApplicationId app, string name = "web") =>
        new(id, app, "Eshop", "acme", "prod", name);

    private static FingerprintQuietWindow Own(ServiceId service, string fingerprint, int minutes) =>
        new()
        {
            ServiceId = service,
            Fingerprint = fingerprint,
            ApplicationId = App,
            QuietWindowMinutes = minutes,
        };

    [Fact]
    public void A_fresh_application_watches_errors_with_the_default_quiet_window()
    {
        var effective = EffectiveSettings.Build([Registered(Web, App)], [], [], []);

        Assert.Equal(Sensitivity.Errors, effective.SensitivityOf(Web));
        Assert.Equal(TimeSpan.FromMinutes(15), effective.QuietWindowOf(Web, Kind));
    }

    [Fact]
    public void An_application_setting_reaches_every_service_without_an_override()
    {
        var effective = EffectiveSettings.Build(
            [Registered(Web, App), Registered(Worker, App, "worker")],
            [new ApplicationAlertingSettings
            {
                ApplicationId = App,
                Sensitivity = Sensitivity.ErrorsAndWarnings,
                QuietWindowMinutes = 30,
            }],
            [],
            []);

        Assert.Equal(Sensitivity.ErrorsAndWarnings, effective.SensitivityOf(Worker));
        Assert.Equal(TimeSpan.FromMinutes(30), effective.QuietWindowOf(Worker, Kind));
    }

    [Fact]
    public void A_service_override_beats_the_application_and_a_null_field_falls_through()
    {
        var effective = EffectiveSettings.Build(
            [Registered(Web, App)],
            [new ApplicationAlertingSettings { ApplicationId = App, QuietWindowMinutes = 45 }],
            [new ServiceAlertingSettings
            {
                ServiceId = Web,
                ApplicationId = App,
                Sensitivity = Sensitivity.Off,
                QuietWindowMinutes = null,
            }],
            []);

        Assert.Equal(Sensitivity.Off, effective.SensitivityOf(Web));
        Assert.Equal(TimeSpan.FromMinutes(45), effective.QuietWindowOf(Web, Kind));
    }

    [Fact]
    public void A_kind_of_trouble_keeps_its_own_window_while_its_siblings_inherit()
    {
        var effective = EffectiveSettings.Build(
            [Registered(Web, App)],
            [new ApplicationAlertingSettings { ApplicationId = App, QuietWindowMinutes = 45 }],
            [new ServiceAlertingSettings
            {
                ServiceId = Web,
                ApplicationId = App,
                QuietWindowMinutes = 20,
            }],
            [Own(Web, Kind, 120)]);

        Assert.Equal(TimeSpan.FromMinutes(120), effective.QuietWindowOf(Web, Kind));
        Assert.Equal(TimeSpan.FromMinutes(20), effective.QuietWindowOf(Web, OtherKind));
        Assert.Equal(20, effective.InheritedQuietWindowMinutesOf(Web));
    }

    [Fact]
    public void A_kind_window_belongs_to_one_service_only()
    {
        var effective = EffectiveSettings.Build(
            [Registered(Web, App), Registered(Worker, App, "worker")],
            [],
            [],
            [Own(Web, Kind, 120)]);

        // The same message template in a sibling Service is a different kind of trouble to tune:
        // ADR 0001 keeps one noisy Service from reaching across its Application.
        Assert.Equal(TimeSpan.FromMinutes(120), effective.QuietWindowOf(Web, Kind));
        Assert.Equal(TimeSpan.FromMinutes(15), effective.QuietWindowOf(Worker, Kind));
    }

    [Fact]
    public void An_unknown_service_never_alerts()
    {
        var effective = EffectiveSettings.Build([Registered(Web, App)], [], [], []);
        var orphan = ServiceId.New();

        Assert.Equal(Sensitivity.Off, effective.SensitivityOf(orphan));
        Assert.Null(effective.ApplicationOf(orphan));
    }

    [Fact]
    public void The_off_list_names_only_the_muted_services_of_the_asked_application()
    {
        var otherApp = ApplicationId.New();
        var otherService = ServiceId.New();
        var effective = EffectiveSettings.Build(
            [Registered(Web, App), Registered(Worker, App, "worker"), Registered(otherService, otherApp)],
            [new ApplicationAlertingSettings { ApplicationId = App, Sensitivity = Sensitivity.Off },
             new ApplicationAlertingSettings { ApplicationId = otherApp, Sensitivity = Sensitivity.Off }],
            [new ServiceAlertingSettings
            {
                ServiceId = Worker,
                ApplicationId = App,
                Sensitivity = Sensitivity.Errors,
            }],
            []);

        Assert.Equal([Web], effective.ServicesEffectivelyOff(App));
    }
}
