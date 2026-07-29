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

    private static EffectiveSettings Defaults(params (ServiceId Id, Sensitivity? Override)[] services) =>
        EffectiveSettings.Build(
            services.Select(s => new CatalogService(s.Id, App, "Eshop", "acme", "prod", "web")).ToList(),
            [],
            services
                .Where(s => s.Override is not null)
                .Select(s => new ServiceAlertingSettings
                {
                    ServiceId = s.Id,
                    ApplicationId = App,
                    Sensitivity = s.Override,
                })
                .ToList());

    private static MatchingLog Log(long id, ServiceId service, short severity) =>
        new(id, service.Value, DateTime.UtcNow, severity, $"log {id}");

    [Fact]
    public void The_first_matching_log_opens_and_later_ones_only_count()
    {
        var decisions = DetectionBatch.Decide(
            [Log(1, Web, 17), Log(2, Web, 21), Log(3, Web, 18)],
            Defaults((Web, null)),
            servicesWithOpenEpisode: new HashSet<ServiceId>(),
            seenIds: new HashSet<long>());

        var detection = Assert.Single(decisions.Services);
        Assert.Equal(1, detection.OpensWith!.Id);
        Assert.Equal(3, detection.ErrorCount);
        Assert.Equal(0, detection.WarnCount);
        Assert.Equal([1, 2, 3], decisions.MatchedIds);
    }

    [Fact]
    public void An_already_open_episode_absorbs_matches_without_reopening()
    {
        var decisions = DetectionBatch.Decide(
            [Log(5, Web, 17)],
            Defaults((Web, null)),
            servicesWithOpenEpisode: new HashSet<ServiceId> { Web },
            seenIds: new HashSet<long>());

        var detection = Assert.Single(decisions.Services);
        Assert.Null(detection.OpensWith);
        Assert.Equal(1, detection.ErrorCount);
    }

    [Fact]
    public void Severity_sixteen_is_a_warning_and_seventeen_an_error()
    {
        var decisions = DetectionBatch.Decide(
            [Log(1, Web, 16), Log(2, Web, 17)],
            Defaults((Web, Sensitivity.ErrorsAndWarnings)),
            new HashSet<ServiceId>(),
            new HashSet<long>());

        var detection = Assert.Single(decisions.Services);
        Assert.Equal(1, detection.ErrorCount);
        Assert.Equal(1, detection.WarnCount);
    }

    [Fact]
    public void The_per_service_floor_filters_what_the_global_poll_admitted()
    {
        // Worker watches warnings, so the poll reads severity >= 13 — but Web still only errors.
        var decisions = DetectionBatch.Decide(
            [Log(1, Web, 14), Log(2, Worker, 14)],
            Defaults((Web, null), (Worker, Sensitivity.ErrorsAndWarnings)),
            new HashSet<ServiceId>(),
            new HashSet<long>());

        var detection = Assert.Single(decisions.Services);
        Assert.Equal(Worker, detection.ServiceId);
        Assert.Equal([2], decisions.MatchedIds);
    }

    [Fact]
    public void A_row_the_overlap_already_processed_is_skipped()
    {
        var decisions = DetectionBatch.Decide(
            [Log(1, Web, 17), Log(2, Web, 17)],
            Defaults((Web, null)),
            new HashSet<ServiceId> { Web },
            seenIds: new HashSet<long> { 1 });

        Assert.Equal([2], decisions.MatchedIds);
        Assert.Equal(1, Assert.Single(decisions.Services).ErrorCount);
    }

    [Fact]
    public void Telemetry_of_an_unregistered_service_never_alerts()
    {
        var orphan = ServiceId.New();
        var decisions = DetectionBatch.Decide(
            [Log(1, orphan, 21)],
            Defaults((Web, null)),
            new HashSet<ServiceId>(),
            new HashSet<long>());

        Assert.Empty(decisions.Services);
        Assert.Empty(decisions.MatchedIds);
    }

    [Fact]
    public void Services_are_judged_independently_within_one_page()
    {
        var decisions = DetectionBatch.Decide(
            [Log(1, Web, 17), Log(2, Worker, 17)],
            Defaults((Web, null), (Worker, null)),
            servicesWithOpenEpisode: new HashSet<ServiceId> { Worker },
            new HashSet<long>());

        Assert.Equal(2, decisions.Services.Count);
        Assert.NotNull(decisions.Services.Single(d => d.ServiceId == Web).OpensWith);
        Assert.Null(decisions.Services.Single(d => d.ServiceId == Worker).OpensWith);
    }
}
