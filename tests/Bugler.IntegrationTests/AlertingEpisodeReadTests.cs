using System.Net;
using System.Net.Http.Json;
using Bugler.Alerting.DescribeEpisode;
using Bugler.Alerting.Episodes;
using Bugler.Alerting.ListEpisodes;
using Bugler.Alerting.ReadEffectiveSensitivity;
using Bugler.Alerting.SummarizeEpisodesByService;

namespace Bugler.IntegrationTests;

/// <summary>
/// The read model behind the triage UI: the list's narrowing filters, the per-state counts, the
/// episode detail with its deliveries and effective settings, and the sensitivity flags that keep
/// a reader from subscribing to silence.
/// </summary>
public sealed class AlertingEpisodeReadTests : IAsyncLifetime
{
    private BuglerHarness _harness = null!;

    public async Task InitializeAsync() => _harness = await BuglerHarness.StartAsync();

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task The_list_narrows_by_services_window_text_and_acknowledgement()
    {
        var (secondService, _) = await _harness.SeedServiceAsync(
            _harness.ApplicationId, "acme", "prod", "worker");
        var (thirdApp, thirdService, _) = await _harness.SeedApplicationAsync(
            "Crm", "acme", "prod", "backend");

        await SeedEpisodeAsync(_harness.ServiceId, _harness.ApplicationId, "Timeout waiting for payment");
        await SeedEpisodeAsync(secondService, _harness.ApplicationId, "Deadlock 502 on ledger");
        await SeedEpisodeAsync(thirdService, thirdApp, "Bulk index rejected 50% of documents",
            daysAgo: 10);

        // serviceId is repeatable — the set is an OR, same shape as state.
        var two = await ListAsync($"?serviceId={_harness.ServiceId}&serviceId={secondService}");
        Assert.Equal(2, two.Items.Count);

        // from: only episodes opened at or after the instant.
        var window = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-7).ToString("O"));
        var recent = await ListAsync($"?from={window}");
        Assert.Equal(2, recent.Items.Count);
        Assert.DoesNotContain(recent.Items, e => e.FirstMatchDetail!.Contains("Bulk index"));

        // q: case-insensitive substring of the first log body.
        var text = await ListAsync("?q=TIMEOUT");
        var timeout = Assert.Single(text.Items);
        Assert.Contains("Timeout", timeout.FirstMatchDetail);

        // LIKE wildcards in q match literally: "50%" must not swallow "502".
        var literal = await ListAsync($"?q={Uri.EscapeDataString("50%")}");
        var escaped = Assert.Single(literal.Items);
        Assert.Contains("50% of documents", escaped.FirstMatchDetail);

        // acknowledged: "me" is the caller's mark, "none" is nobody's; anything else is an error.
        await _harness.Client.PostAsync($"/api/alerting/episodes/{timeout.Id}/acknowledge", null);
        var mine = await ListAsync("?acknowledged=me");
        Assert.Equal(timeout.Id, Assert.Single(mine.Items).Id);
        var nobodys = await ListAsync("?acknowledged=none");
        Assert.Equal(2, nobodys.Items.Count);
        Assert.DoesNotContain(nobodys.Items, e => e.Id == timeout.Id);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await _harness.Client.GetAsync("/api/alerting/episodes?acknowledged=somebody")).StatusCode);
    }

    [Fact]
    public async Task Counts_break_the_visible_filtered_set_down_by_state()
    {
        var (foreignApp, foreignService, _) = await _harness.SeedApplicationAsync(
            "Crm", "acme", "prod", "backend");
        await SeedEpisodeAsync(_harness.ServiceId, _harness.ApplicationId, "open trouble");
        await SeedEpisodeAsync(_harness.ServiceId, _harness.ApplicationId, "quiet trouble", quieted: true);
        await SeedEpisodeAsync(_harness.ServiceId, _harness.ApplicationId, "muted trouble", muted: true);
        await SeedEpisodeAsync(_harness.ServiceId, _harness.ApplicationId, "solved trouble");
        await SeedEpisodeAsync(foreignService, foreignApp, "foreign trouble");

        var solvedId = (await ListAsync("?q=solved")).Items[0].Id;
        await _harness.Client.PostAsync($"/api/alerting/episodes/{solvedId}/solve", null);

        // The admin's whole picture, one number per state.
        var all = await CountsAsync("");
        Assert.Equal(new EpisodeCountsResponse(2, 1, 1, 1), all);

        // The same filters as the list narrow the breakdown.
        var quiet = await CountsAsync("?q=quiet");
        Assert.Equal(new EpisodeCountsResponse(0, 1, 0, 0), quiet);

        // A member counts only what they may see; a stranger counts nothing.
        var member = await _harness.CreateUserClientAsync(
            "member@bugler.test", "MemberPass123!", _harness.ApplicationId);
        var scoped = await member.GetFromJsonAsync<EpisodeCountsResponse>(
            "/api/alerting/episodes/counts");
        Assert.Equal(new EpisodeCountsResponse(1, 1, 1, 1), scoped);
        var stranger = await _harness.CreateUserClientAsync("stranger@bugler.test", "Stranger123!");
        var nothing = await stranger.GetFromJsonAsync<EpisodeCountsResponse>(
            "/api/alerting/episodes/counts");
        Assert.Equal(new EpisodeCountsResponse(0, 0, 0, 0), nothing);
    }

    [Fact]
    public async Task By_service_quotes_the_newest_open_episode_or_the_newest_closed_one()
    {
        var (secondService, _) = await _harness.SeedServiceAsync(
            _harness.ApplicationId, "acme", "prod", "worker");
        var (foreignApp, foreignService, _) = await _harness.SeedApplicationAsync(
            "Crm", "acme", "prod", "backend");

        await SeedEpisodeAsync(_harness.ServiceId, _harness.ApplicationId, "older open trouble");
        await SeedEpisodeAsync(_harness.ServiceId, _harness.ApplicationId, "quiet stretch", quieted: true);
        await SeedEpisodeAsync(_harness.ServiceId, _harness.ApplicationId, "newest open trouble");
        await SeedEpisodeAsync(secondService, _harness.ApplicationId, "worker settled", quieted: true);
        await SeedEpisodeAsync(foreignService, foreignApp, "foreign trouble");

        var all = await ByServiceAsync("");
        Assert.Equal(3, all.Services.Count);

        var web = all.Services.Single(s => s.ServiceId == _harness.ServiceId);
        Assert.Equal((2, 1, 0, 0), (web.Open, web.Quieted, web.Solved, web.Muted));
        // An open episode outranks any closed one, and among the open the newest wins.
        Assert.Equal(EpisodeState.Open, web.Latest!.State);
        Assert.Equal("newest open trouble", web.Latest.FirstMatchDetail);

        // Nothing open on the worker, so it is quoted by when its last stretch ended.
        var worker = all.Services.Single(s => s.ServiceId == secondService);
        Assert.Equal((0, 1, 0, 0), (worker.Open, worker.Quieted, worker.Solved, worker.Muted));
        Assert.Equal(EpisodeState.Quieted, worker.Latest!.State);
        Assert.NotNull(worker.Latest.ClosedAt);

        // A member's board holds only their applications' services; a foreign one never leaks in.
        var member = await _harness.CreateUserClientAsync(
            "member@bugler.test", "MemberPass123!", _harness.ApplicationId);
        var scoped = await member.GetFromJsonAsync<EpisodesByServiceResponse>(
            "/api/alerting/episodes/by-service");
        Assert.Equal(2, scoped!.Services.Count);
        Assert.DoesNotContain(scoped.Services, s => s.ServiceId == foreignService);
    }

    [Fact]
    public async Task By_service_narrows_by_the_window_and_shows_a_stranger_nothing()
    {
        await SeedEpisodeAsync(_harness.ServiceId, _harness.ApplicationId, "ancient trouble", daysAgo: 10);
        await SeedEpisodeAsync(_harness.ServiceId, _harness.ApplicationId, "fresh trouble");

        var window = Uri.EscapeDataString(DateTimeOffset.UtcNow.AddDays(-7).ToString("O"));
        var recent = await ByServiceAsync($"?from={window}");
        var web = Assert.Single(recent.Services);
        // The ancient one is still open, but it opened before the window: not this board's story.
        Assert.Equal(1, web.Open);
        Assert.Equal("fresh trouble", web.Latest!.FirstMatchDetail);

        var stranger = await _harness.CreateUserClientAsync("stranger@bugler.test", "Stranger123!");
        var nothing = await stranger.GetFromJsonAsync<EpisodesByServiceResponse>(
            "/api/alerting/episodes/by-service");
        Assert.Empty(nothing!.Services);
    }

    [Fact]
    public async Task The_detail_reads_deliveries_recurrence_and_the_effective_settings()
    {
        await SeedEpisodeAsync(_harness.ServiceId, _harness.ApplicationId, "boom", quieted: true);
        await SeedEpisodeAsync(_harness.ServiceId, _harness.ApplicationId, "boom");
        var id = (await ListAsync("?state=Open")).Items[0].Id;

        // Two mailed Alerts (one landed, one still pending) and a chat Alert that landed.
        await SeedAlertDeliveryAsync(id, channel: 1, userId: Guid.NewGuid(), deliveredMinutesAgo: 5);
        await SeedAlertDeliveryAsync(id, channel: 1, userId: Guid.NewGuid(), deliveredMinutesAgo: null);
        await SeedAlertDeliveryAsync(id, channel: 2, userId: null, deliveredMinutesAgo: 4);

        // The detail must resolve the service override, not the default.
        var put = await _harness.Client.PutAsJsonAsync(
            $"/api/admin/services/{_harness.ServiceId}/alerting",
            new { sensitivity = "ErrorsAndWarnings", quietWindowMinutes = 25 });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var detail = await _harness.Client.GetFromJsonAsync<EpisodeDetailDto>(
            $"/api/alerting/episodes/{id}/detail");
        Assert.Equal(id, detail!.Episode.Id);
        Assert.Equal(1, detail.Episode.PriorCount);
        Assert.Equal(2, detail.MailAlert!.SubscriberCount);
        Assert.NotNull(detail.MailAlert.FirstDeliveredAt);
        Assert.NotNull(detail.ChatAlert!.DeliveredAt);
        Assert.Equal("ErrorsAndWarnings", detail.EffectiveSensitivity.ToString());
        Assert.Equal(25, detail.QuietWindowMinutes);

        // An episode nothing was owed about reports no alerts, not zeros.
        var quietedId = (await ListAsync("?state=Quieted")).Items[0].Id;
        var bare = await _harness.Client.GetFromJsonAsync<EpisodeDetailDto>(
            $"/api/alerting/episodes/{quietedId}/detail");
        Assert.Null(bare!.MailAlert);
        Assert.Null(bare.ChatAlert);

        // Outside the caller's scope the detail 404s: not yours to see, not yours to know about.
        var stranger = await _harness.CreateUserClientAsync("stranger@bugler.test", "Stranger123!");
        Assert.Equal(HttpStatusCode.NotFound,
            (await stranger.GetAsync($"/api/alerting/episodes/{id}/detail")).StatusCode);
    }

    [Fact]
    public async Task Effective_sensitivity_flags_the_services_that_do_not_alert()
    {
        var (_, foreignService, _) = await _harness.SeedApplicationAsync(
            "Crm", "acme", "prod", "backend");
        var (overriding, _) = await _harness.SeedServiceAsync(
            _harness.ApplicationId, "acme", "prod", "worker");

        // Application-wide Off, with one service overriding itself back on.
        await _harness.Client.PutAsJsonAsync(
            $"/api/admin/applications/{_harness.ApplicationId}/alerting",
            new { sensitivity = "Off", quietWindowMinutes = (int?)null });
        await _harness.Client.PutAsJsonAsync(
            $"/api/admin/services/{overriding}/alerting",
            new { sensitivity = "Errors", quietWindowMinutes = (int?)null });

        var all = await _harness.Client.GetFromJsonAsync<ListSensitivityResponse>(
            "/api/alerting/sensitivity");
        Assert.True(all!.Items.Single(i => i.ServiceId == _harness.ServiceId).Off);
        Assert.False(all.Items.Single(i => i.ServiceId == overriding).Off);
        Assert.False(all.Items.Single(i => i.ServiceId == foreignService).Off);

        // A member reads only the services of their applications.
        var member = await _harness.CreateUserClientAsync(
            "member@bugler.test", "MemberPass123!", _harness.ApplicationId);
        var scoped = await member.GetFromJsonAsync<ListSensitivityResponse>(
            "/api/alerting/sensitivity");
        Assert.Equal(2, scoped!.Items.Count);
        Assert.DoesNotContain(scoped.Items, i => i.ServiceId == foreignService);
    }

    private async Task<ListEpisodesResponse> ListAsync(string query) =>
        (await _harness.Client.GetFromJsonAsync<ListEpisodesResponse>(
            $"/api/alerting/episodes{query}"))!;

    private async Task<EpisodeCountsResponse> CountsAsync(string query) =>
        (await _harness.Client.GetFromJsonAsync<EpisodeCountsResponse>(
            $"/api/alerting/episodes/counts{query}"))!;

    private async Task<EpisodesByServiceResponse> ByServiceAsync(string query) =>
        (await _harness.Client.GetFromJsonAsync<EpisodesByServiceResponse>(
            $"/api/alerting/episodes/by-service{query}"))!;

    private Task SeedEpisodeAsync(
        Guid serviceId, Guid applicationId, string body,
        bool quieted = false, bool muted = false, int daysAgo = 0) =>
        // Version-7 id from the client — the ordering the endpoint's keyset rides on. The body
        // doubles as the Fingerprint, as it does for template-less logs in detection.
        _harness.ExecuteSqlAsync(
            $"""
            INSERT INTO alerting.episodes
                (id, service_id, application_id, watch, fingerprint, opened_at, first_match_log_id,
                 first_match_at, first_match_severity, first_match_detail, error_count,
                 warn_count, last_match_at, closed_at, close_reason)
            VALUES
                ('{Guid.CreateVersion7()}', '{serviceId}', '{applicationId}', 1, '{body}',
                 now() - interval '{daysAgo} days', 1, now(), 17, '{body}', 1, 0, now(),
                 {(quieted || muted ? "now()" : "NULL")}, {(quieted ? "1" : muted ? "2" : "NULL")})
            """);

    private Task SeedAlertDeliveryAsync(
        Guid episodeId, short channel, Guid? userId, int? deliveredMinutesAgo) =>
        _harness.ExecuteSqlAsync(
            $"""
            INSERT INTO alerting.deliveries
                (id, episode_id, kind, channel, user_id, attempts, created_at, next_attempt_at,
                 delivered_at, lapsed_at, last_error)
            VALUES
                ('{Guid.CreateVersion7()}', '{episodeId}', 1, {channel},
                 {(userId is null ? "NULL" : $"'{userId}'")}, 1, now(), now(),
                 {(deliveredMinutesAgo is null
                     ? "NULL"
                     : $"now() - interval '{deliveredMinutesAgo} minutes'")}, NULL, NULL)
            """);
}
