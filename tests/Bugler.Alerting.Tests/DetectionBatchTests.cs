using Bugler.Alerting.DetectEpisodes;
using Bugler.Alerting.Settings;
using Bugler.Registry.Contracts;
using Bugler.SharedKernel;

namespace Bugler.Alerting.Tests;

public class DetectionBatchTests
{
    private static readonly ApplicationId App = ApplicationId.New();
    private static readonly ServiceId Web = ServiceId.New();
    private static readonly ServiceId Worker = ServiceId.New();

    private static readonly HashSet<(string, string)> NothingOpen = [];

    private static CatalogService Registered(
        ServiceId id, string environment = "prod", string name = "web") =>
        new(id, App, "Eshop", "acme", environment, name);

    private static EffectiveSettings Defaults(params (ServiceId Id, Sensitivity? Override)[] services) =>
        Settings(services.Select(s => Registered(s.Id)).ToList(), null, services);

    private static EffectiveSettings Settings(
        IReadOnlyList<CatalogService> catalog,
        ApplicationAlertingSettings? application,
        (ServiceId Id, Sensitivity? Override)[] services) =>
        EffectiveSettings.Build(
            catalog,
            application is null ? [] : [application],
            services
                .Where(s => s.Override is not null)
                .Select(s => new ServiceAlertingSettings
                {
                    ServiceId = s.Id,
                    ApplicationId = App,
                    Sensitivity = s.Override,
                })
                .ToList(),
            []);

    private static MatchingLog Log(
        long id,
        ServiceId service,
        short severity,
        string body = "boom",
        string? version = null,
        string? stack = null,
        string? runtime = null,
        string? type = null) =>
        new(id, service.Value, DateTime.UtcNow, severity, body, Template: null, EventName: null,
            ExceptionType: type, ExceptionStack: stack, Runtime: runtime, ServiceVersion: version);

    /// <summary>What the default Scope binds a Service to: its Application and its Environment.</summary>
    private static string ScopeOf(string environment = "prod") =>
        EpisodeScope.Default.KeyOf(Registered(Web, environment));

    [Fact]
    public void The_first_matching_log_opens_and_later_ones_of_the_same_kind_only_count()
    {
        var decisions = DetectionBatch.Decide(
            [Log(1, Web, 17), Log(2, Web, 21), Log(3, Web, 18)],
            Defaults((Web, null)),
            NothingOpen,
            seenIds: new HashSet<long>());

        var detection = Assert.Single(decisions.Scopes);
        Assert.Equal(1, detection.OpensWith!.Id);
        Assert.Equal(3, detection.ErrorCount);
        Assert.Equal(0, detection.WarnCount);
        Assert.Equal([1, 2, 3], decisions.MatchedIds);
    }

    [Fact]
    public void A_different_kind_of_trouble_opens_its_own_episode()
    {
        var decisions = DetectionBatch.Decide(
            [Log(1, Web, 17, "payment declined"), Log(2, Web, 17, "warehouse timeout")],
            Defaults((Web, null)),
            NothingOpen,
            new HashSet<long>());

        Assert.Equal(2, decisions.Scopes.Count);
        Assert.All(decisions.Scopes, d => Assert.NotNull(d.OpensWith));
    }

    [Fact]
    public void An_already_open_episode_absorbs_matches_of_its_kind_without_reopening()
    {
        var fingerprint = Fingerprint.Of(
            new FingerprintEvidence(null, null, "boom", null, null, null),
            FingerprintRule.ThrowingCode).Fingerprint;

        var decisions = DetectionBatch.Decide(
            [Log(5, Web, 17)],
            Defaults((Web, null)),
            new HashSet<(string, string)> { (ScopeOf(), fingerprint) },
            new HashSet<long>());

        var detection = Assert.Single(decisions.Scopes);
        Assert.Null(detection.OpensWith);
        Assert.Equal(1, detection.ErrorCount);
    }

    // ---- The Scope (ADR 0034) ----------------------------------------------------------------

    [Fact]
    public void Two_services_of_one_application_on_one_kind_of_trouble_share_one_episode()
    {
        var decisions = DetectionBatch.Decide(
            [Log(1, Web, 17, version: "1.4.0"), Log(2, Worker, 17, version: "1.5.0")],
            Settings([Registered(Web), Registered(Worker, name: "worker")], null,
                [(Web, null), (Worker, null)]),
            NothingOpen,
            new HashSet<long>());

        var detection = Assert.Single(decisions.Scopes);
        Assert.Equal(2, detection.ErrorCount);
        Assert.Equal(
            [(Web, "1.4.0"), (Worker, "1.5.0")],
            detection.Participants
                .Select(p => (p.ServiceId, p.Version))
                .OrderBy(p => p.Version, StringComparer.Ordinal)
                .ToList());
    }

    [Fact]
    public void Two_environments_never_meet_because_environment_stands_by_default()
    {
        var staging = ServiceId.New();
        var decisions = DetectionBatch.Decide(
            [Log(1, Web, 17), Log(2, staging, 17)],
            Settings([Registered(Web), Registered(staging, environment: "staging")], null,
                [(Web, null), (staging, null)]),
            NothingOpen,
            new HashSet<long>());

        Assert.Equal(2, decisions.Scopes.Count);
        Assert.Equal(2, decisions.Scopes.Select(d => d.ScopeKey).Distinct().Count());
    }

    [Fact]
    public void A_scope_that_also_holds_the_service_name_keeps_two_roles_apart()
    {
        var decisions = DetectionBatch.Decide(
            [Log(1, Web, 17), Log(2, Worker, 17)],
            Settings(
                [Registered(Web), Registered(Worker, name: "worker")],
                new ApplicationAlertingSettings
                {
                    ApplicationId = App,
                    ScopeByServiceName = true,
                    ScopeByEnvironment = true,
                },
                [(Web, null), (Worker, null)]),
            NothingOpen,
            new HashSet<long>());

        Assert.Equal(2, decisions.Scopes.Count);
    }

    [Fact]
    public void One_service_shipping_two_versions_is_two_participations_of_one_episode()
    {
        var decisions = DetectionBatch.Decide(
            [Log(1, Web, 17, version: "1.4.0"), Log(2, Web, 17, version: "1.5.0")],
            Defaults((Web, null)),
            NothingOpen,
            new HashSet<long>());

        var detection = Assert.Single(decisions.Scopes);
        Assert.Equal(2, detection.Participants.Count);
        Assert.All(detection.Participants, p => Assert.Equal(1, p.ErrorCount));
    }

    [Fact]
    public void A_sender_that_declares_no_version_is_one_participant_rather_than_one_per_match()
    {
        var decisions = DetectionBatch.Decide(
            [Log(1, Web, 17), Log(2, Web, 17)],
            Defaults((Web, null)),
            NothingOpen,
            new HashSet<long>());

        var participant = Assert.Single(Assert.Single(decisions.Scopes).Participants);
        Assert.Null(participant.Version);
        Assert.Equal(2, participant.ErrorCount);
    }

    // ---- No cap (ADR 0034) -------------------------------------------------------------------

    [Fact]
    public void Nothing_folds_however_many_kinds_a_scope_already_holds_open()
    {
        var alreadyOpen = Enumerable.Range(0, 200)
            .Select(i => (ScopeOf(), $"kind-{i}"))
            .ToHashSet();

        var decisions = DetectionBatch.Decide(
            [Log(1, Web, 17, "yet another kind"), Log(2, Web, 17, "and one more")],
            Defaults((Web, null)),
            alreadyOpen,
            new HashSet<long>());

        Assert.Equal(2, decisions.Scopes.Count);
        Assert.All(decisions.Scopes, d => Assert.NotNull(d.OpensWith));
    }

    // ---- What was already true -----------------------------------------------------------------

    [Fact]
    public void Severity_sixteen_is_a_warning_and_seventeen_an_error()
    {
        var decisions = DetectionBatch.Decide(
            [Log(1, Web, 16), Log(2, Web, 17)],
            Defaults((Web, Sensitivity.ErrorsAndWarnings)),
            NothingOpen,
            new HashSet<long>());

        var detection = Assert.Single(decisions.Scopes);
        Assert.Equal(1, detection.ErrorCount);
        Assert.Equal(1, detection.WarnCount);
    }

    [Fact]
    public void The_per_service_floor_filters_what_the_global_poll_admitted()
    {
        // Worker watches warnings, so the poll reads severity >= 13 — but Web still only errors.
        var decisions = DetectionBatch.Decide(
            [Log(1, Web, 14), Log(2, Worker, 14)],
            Settings([Registered(Web), Registered(Worker, name: "worker")], null,
                [(Web, null), (Worker, Sensitivity.ErrorsAndWarnings)]),
            NothingOpen,
            new HashSet<long>());

        var detection = Assert.Single(decisions.Scopes);
        Assert.Equal(Worker, Assert.Single(detection.Participants).ServiceId);
        Assert.Equal([2], decisions.MatchedIds);
    }

    [Fact]
    public void A_row_the_overlap_already_processed_is_skipped()
    {
        var decisions = DetectionBatch.Decide(
            [Log(1, Web, 17), Log(2, Web, 17)],
            Defaults((Web, null)),
            NothingOpen,
            seenIds: new HashSet<long> { 1 });

        Assert.Equal([2], decisions.MatchedIds);
        Assert.Equal(1, Assert.Single(decisions.Scopes).ErrorCount);
    }

    [Fact]
    public void Telemetry_of_an_unregistered_service_never_alerts()
    {
        var orphan = ServiceId.New();
        var decisions = DetectionBatch.Decide(
            [Log(1, orphan, 21)],
            Defaults((Web, null)),
            NothingOpen,
            new HashSet<long>());

        Assert.Empty(decisions.Scopes);
        Assert.Empty(decisions.MatchedIds);
    }

    // ---- The recipe reaches detection ---------------------------------------------------------

    [Fact]
    public void One_sentence_from_two_call_sites_opens_two_episodes()
    {
        MatchingLog Thrown(long id, string method) => Log(
            id, Web, 17, "MongoDb transaction commit error",
            stack: $"System.Exception: boom\n   at {method}(Order o) in /src/A.cs:line 3",
            runtime: "dotnet",
            type: "MongoDB.Driver.MongoException");

        var decisions = DetectionBatch.Decide(
            [Thrown(1, "Acme.Checkout.Commit"), Thrown(2, "Acme.Warehouse.Reserve")],
            Defaults((Web, null)),
            NothingOpen,
            new HashSet<long>());

        Assert.Equal(2, decisions.Scopes.Count);
        Assert.All(decisions.Scopes, d => Assert.Equal(FingerprintRung.Stack, d.Rung));
    }

    [Fact]
    public void The_opening_match_stamps_the_title_and_the_rung_on_the_episode()
    {
        var decisions = DetectionBatch.Decide(
            [Log(1, Web, 17, "boom", type: "System.TimeoutException")],
            Defaults((Web, null)),
            NothingOpen,
            new HashSet<long>());

        var detection = Assert.Single(decisions.Scopes);
        Assert.Equal("TimeoutException: boom", detection.Title);
        Assert.Equal(FingerprintRung.Failure, detection.Rung);
        Assert.False(detection.StackTruncated);
    }
}
