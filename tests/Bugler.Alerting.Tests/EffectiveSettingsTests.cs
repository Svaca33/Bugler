using Bugler.Alerting.Settings;
using Bugler.Registry.Contracts;
using Bugler.SharedKernel;

namespace Bugler.Alerting.Tests;

public class EffectiveSettingsTests
{
    private static readonly ApplicationId App = ApplicationId.New();
    private static readonly ServiceId Web = ServiceId.New();
    private static readonly ServiceId Worker = ServiceId.New();

    private static CatalogService Registered(ServiceId id, ApplicationId app, string name = "web") =>
        new(id, app, "Eshop", "acme", "prod", name);

    [Fact]
    public void A_fresh_application_watches_errors_with_the_default_quiet_window()
    {
        var effective = EffectiveSettings.Build([Registered(Web, App)], [], []);

        Assert.Equal(Sensitivity.Errors, effective.SensitivityOf(Web));
        Assert.Equal(TimeSpan.FromMinutes(15), effective.QuietWindowOf(Web));
        Assert.Equal((short)17, effective.GlobalSeverityFloor);
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
            []);

        Assert.Equal(Sensitivity.ErrorsAndWarnings, effective.SensitivityOf(Worker));
        Assert.Equal(TimeSpan.FromMinutes(30), effective.QuietWindowOf(Worker));
        Assert.Equal((short)13, effective.GlobalSeverityFloor);
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
            }]);

        Assert.Equal(Sensitivity.Off, effective.SensitivityOf(Web));
        Assert.Equal(TimeSpan.FromMinutes(45), effective.QuietWindowOf(Web));
    }

    [Fact]
    public void The_global_floor_is_the_lowest_any_watched_service_wants()
    {
        var effective = EffectiveSettings.Build(
            [Registered(Web, App), Registered(Worker, App, "worker")],
            [],
            [new ServiceAlertingSettings
            {
                ServiceId = Worker,
                ApplicationId = App,
                Sensitivity = Sensitivity.ErrorsAndWarnings,
            }]);

        Assert.Equal((short)13, effective.GlobalSeverityFloor);
    }

    [Fact]
    public void When_every_service_is_off_there_is_nothing_to_poll_for()
    {
        var effective = EffectiveSettings.Build(
            [Registered(Web, App)],
            [new ApplicationAlertingSettings { ApplicationId = App, Sensitivity = Sensitivity.Off }],
            []);

        Assert.Null(effective.GlobalSeverityFloor);
    }

    [Fact]
    public void An_unknown_service_never_alerts()
    {
        var effective = EffectiveSettings.Build([Registered(Web, App)], [], []);
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
            }]);

        Assert.Equal([Web], effective.ServicesEffectivelyOff(App));
    }
}
