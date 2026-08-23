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

    /// <summary>The Scope a Service falls into under the default Episode Scope: Application and Environment.</summary>
    private static string ScopeOf(ServiceId service, ApplicationId app = default) =>
        EpisodeScope.Default.KeyOf(Registered(service, app == default ? App : app));

    private static FingerprintQuietWindow Own(ServiceId service, string fingerprint, int minutes) =>
        new()
        {
            ScopeKey = ScopeOf(service),
            Fingerprint = fingerprint,
            ApplicationId = App,
            QuietWindowMinutes = minutes,
        };

    [Fact]
    public void A_fresh_application_watches_errors_with_the_default_quiet_window()
    {
        var effective = EffectiveSettings.Build([Registered(Web, App)], [], [], []);

        Assert.Equal(Sensitivity.Errors, effective.SensitivityOf(Web));
        Assert.Equal(TimeSpan.FromMinutes(15), effective.QuietWindowOf(Web, ScopeOf(Web), Kind));
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
        Assert.Equal(TimeSpan.FromMinutes(30), effective.QuietWindowOf(Worker, ScopeOf(Worker), Kind));
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
        Assert.Equal(TimeSpan.FromMinutes(45), effective.QuietWindowOf(Web, ScopeOf(Web), Kind));
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

        Assert.Equal(TimeSpan.FromMinutes(120), effective.QuietWindowOf(Web, ScopeOf(Web), Kind));
        Assert.Equal(TimeSpan.FromMinutes(20), effective.QuietWindowOf(Web, ScopeOf(Web), OtherKind));
        Assert.Equal(20, effective.InheritedQuietWindowMinutesOf(Web));
    }

    [Fact]
    public void A_kind_window_belongs_to_one_episode_scope()
    {
        // Since ADR 0034 the window follows the Episode: two Services that share a Scope share
        // the one kind of trouble, so they share the window set on it. A Service in another
        // Environment is another Scope, and inherits.
        var staging = ServiceId.New();
        var effective = EffectiveSettings.Build(
            [
                Registered(Web, App),
                Registered(Worker, App, "worker"),
                new CatalogService(staging, App, "Eshop", "acme", "staging", "web"),
            ],
            [],
            [],
            [Own(Web, Kind, 120)]);

        Assert.Equal(TimeSpan.FromMinutes(120), effective.QuietWindowOf(Web, ScopeOf(Web), Kind));
        Assert.Equal(
            TimeSpan.FromMinutes(120), effective.QuietWindowOf(Worker, ScopeOf(Worker), Kind));
        Assert.Equal(
            TimeSpan.FromMinutes(15),
            effective.QuietWindowOf(staging, effective.ScopeKeyOf(staging)!, Kind));
    }

    [Fact]
    public void The_scope_key_holds_the_facets_the_application_says_must_match()
    {
        var staging = ServiceId.New();
        var catalog = new List<CatalogService>
        {
            Registered(Web, App),
            Registered(Worker, App, "worker"),
            new(staging, App, "Eshop", "acme", "staging", "web"),
        };

        var byDefault = EffectiveSettings.Build(catalog, [], [], []);
        Assert.Equal(byDefault.ScopeKeyOf(Web), byDefault.ScopeKeyOf(Worker));
        Assert.NotEqual(byDefault.ScopeKeyOf(Web), byDefault.ScopeKeyOf(staging));

        var byName = EffectiveSettings.Build(
            catalog,
            [new ApplicationAlertingSettings
            {
                ApplicationId = App,
                ScopeByServiceName = true,
                ScopeByEnvironment = false,
            }],
            [],
            []);
        Assert.NotEqual(byName.ScopeKeyOf(Web), byName.ScopeKeyOf(Worker));
        // Environment was not asked for this time, so the two deployments of `web` do meet.
        Assert.Equal(byName.ScopeKeyOf(Web), byName.ScopeKeyOf(staging));
    }

    [Fact]
    public void The_fingerprint_rule_is_the_applications_and_no_service_overrides_it()
    {
        var effective = EffectiveSettings.Build(
            [Registered(Web, App), Registered(Worker, App, "worker")],
            [new ApplicationAlertingSettings
            {
                ApplicationId = App,
                FingerprintRule = FingerprintRule.WhatWasSaid,
                FingerprintAttributeKey = " acme.error_code ",
            }],
            [new ServiceAlertingSettings { ServiceId = Web, ApplicationId = App }],
            []);

        Assert.Equal(FingerprintRule.WhatWasSaid, effective.FingerprintRuleOf(Web));
        Assert.Equal(FingerprintRule.WhatWasSaid, effective.FingerprintRuleOf(Worker));
        Assert.Equal("acme.error_code", effective.FingerprintAttributeKeyOf(Web));
        Assert.Equal(["acme.error_code"], effective.NamedAttributeKeys());
    }

    [Fact]
    public void An_application_that_names_no_attribute_costs_the_poll_no_extra_column()
    {
        var effective = EffectiveSettings.Build([Registered(Web, App)], [], [], []);

        Assert.Equal(AlertingDefaults.FingerprintRule, effective.FingerprintRuleOf(Web));
        Assert.Null(effective.FingerprintAttributeKeyOf(Web));
        Assert.Empty(effective.NamedAttributeKeys());
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
