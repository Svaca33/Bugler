using System.Net;
using System.Net.Http.Json;
using Bugler.Alerting.CloseQuietEpisodes;
using Bugler.Alerting.DescribeEpisode;
using Bugler.Alerting.ListEpisodes;

namespace Bugler.IntegrationTests;

/// <summary>
/// The third tier of the Quiet Window (ADR 0004): one kind of trouble in one Service keeping its
/// own, set through any Episode of that kind and outliving it.
/// </summary>
public sealed class AlertingFingerprintQuietWindowTests : IAsyncLifetime
{
    private const string Kind = "payment declined";
    private const string OtherKind = "warehouse timed out";

    private BuglerHarness _harness = null!;

    public async Task InitializeAsync() => _harness = await BuglerHarness.StartAsync();

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task A_window_set_on_an_episode_round_trips_and_clearing_it_deletes_the_row()
    {
        var episodeId = await SeedEpisodeAsync(Kind);

        var put = await SetAsync(episodeId, 120);
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);

        var detail = await GetDetailAsync(episodeId);
        Assert.Equal(120, detail.Episode.FingerprintQuietWindowMinutes);
        Assert.Equal(120, detail.QuietWindowMinutes);
        // The Service's own value stays visible, so the panel can name what is being replaced.
        Assert.Equal(15, detail.InheritedQuietWindowMinutes);

        var list = await _harness.Client.GetFromJsonAsync<ListEpisodesResponse>(
            "/api/alerting/episodes");
        Assert.Equal(120, Assert.Single(list!.Items).FingerprintQuietWindowMinutes);

        var cleared = await SetAsync(episodeId, null);
        Assert.Equal(HttpStatusCode.NoContent, cleared.StatusCode);
        await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.fingerprint_quiet_windows", 0);

        var inherited = await GetDetailAsync(episodeId);
        Assert.Null(inherited.Episode.FingerprintQuietWindowMinutes);
        Assert.Equal(15, inherited.QuietWindowMinutes);
    }

    [Fact]
    public async Task Only_one_kind_of_trouble_in_one_episode_scope_is_tuned()
    {
        var (siblingApp, siblingService, _) = await _harness.SeedApplicationAsync(
            "Eshop2", "acme", "prod", "worker");
        var tuned = await SeedEpisodeAsync(Kind);
        var sibling = await SeedEpisodeAsync(OtherKind);
        var elsewhere = await SeedEpisodeAsync(Kind, siblingService, siblingApp);

        Assert.Equal(HttpStatusCode.NoContent, (await SetAsync(tuned, 120)).StatusCode);

        Assert.Equal(120, (await GetDetailAsync(tuned)).QuietWindowMinutes);
        // Another kind in the same Scope, and the same kind in another Application's Scope, both
        // inherit: ADR 0001 keeps one noisy kind from reaching past itself.
        Assert.Equal(15, (await GetDetailAsync(sibling)).QuietWindowMinutes);
        Assert.Equal(15, (await GetDetailAsync(elsewhere)).QuietWindowMinutes);
    }

    [Fact]
    public async Task The_window_is_bounded_by_a_minute_and_by_a_week()
    {
        var episodeId = await SeedEpisodeAsync(Kind);

        Assert.Equal(HttpStatusCode.BadRequest, (await SetAsync(episodeId, 0)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SetAsync(episodeId, 10_081)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SetAsync(episodeId, 1)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SetAsync(episodeId, 10_080)).StatusCode);
    }

    [Fact]
    public async Task An_unknown_episode_is_not_found_and_a_member_may_not_configure()
    {
        var episodeId = await SeedEpisodeAsync(Kind);

        var missing = await SetAsync(episodeId: Guid.NewGuid(), 60);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var member = await _harness.CreateUserClientAsync(
            "member@bugler.test", "MemberPass123!", _harness.ApplicationId);
        var refused = await member.PutAsJsonAsync(
            $"/api/admin/episodes/{episodeId}/quiet-window", new { quietWindowMinutes = (int?)60 });
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
    }

    [Fact]
    public async Task An_episode_from_before_grouping_by_kind_carries_no_window()
    {
        var legacy = await SeedEpisodeAsync(fingerprint: string.Empty);

        var refused = await SetAsync(legacy, 60);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
    }

    [Fact]
    public async Task A_window_set_on_a_closed_episode_governs_the_next_one_of_its_kind()
    {
        // The situation the feature exists for: the mails already came, so every Episode of the
        // annoying kind is Quieted by the time anyone reaches for the knob.
        var closed = await SeedEpisodeAsync(Kind, quieted: true);

        Assert.Equal(HttpStatusCode.NoContent, (await SetAsync(closed, 120)).StatusCode);

        var next = await SeedEpisodeAsync(Kind);
        Assert.Equal(120, (await GetDetailAsync(next)).QuietWindowMinutes);
    }

    [Fact]
    public async Task The_closer_holds_a_tuned_kind_open_while_its_sibling_quiets()
    {
        var tuned = await SeedEpisodeAsync(Kind);
        var sibling = await SeedEpisodeAsync(OtherKind);
        Assert.Equal(HttpStatusCode.NoContent, (await SetAsync(tuned, 120)).StatusCode);

        // Both fell silent an hour ago: past the Service's 15 minutes, inside the tuned 120.
        await _harness.ExecuteSqlAsync(
            "UPDATE alerting.episodes SET last_match_at = now() - interval '1 hour'");
        await _harness.GetRequiredService<EpisodeCloser>().CloseOnceAsync(CancellationToken.None);

        Assert.Equal("Open", (await GetDetailAsync(tuned)).Episode.State.ToString());
        Assert.Equal("Quieted", (await GetDetailAsync(sibling)).Episode.State.ToString());
    }

    [Fact]
    public async Task A_tuned_kind_outlives_a_deleted_service_and_dies_with_its_application()
    {
        var episodeId = await SeedEpisodeAsync(Kind);
        Assert.Equal(HttpStatusCode.NoContent, (await SetAsync(episodeId, 120)).StatusCode);

        // The window is the Episode Scope's since ADR 0034, and a Scope outlives any one of its
        // Services — the next deployment to log this kind falls under the same tuning.
        Assert.Equal(HttpStatusCode.NoContent,
            (await _harness.Client.DeleteAsync($"/api/admin/services/{_harness.ServiceId}")).StatusCode);
        Assert.Equal(1, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.fingerprint_quiet_windows", 1));

        Assert.Equal(HttpStatusCode.NoContent,
            (await _harness.Client.DeleteAsync(
                $"/api/admin/applications/{_harness.ApplicationId}")).StatusCode);
        Assert.Equal(0, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.fingerprint_quiet_windows", 0));
    }

    private Task<HttpResponseMessage> SetAsync(Guid episodeId, int? minutes) =>
        _harness.Client.PutAsJsonAsync(
            $"/api/admin/episodes/{episodeId}/quiet-window",
            new { quietWindowMinutes = minutes });

    private async Task<EpisodeDetailDto> GetDetailAsync(Guid episodeId)
    {
        var detail = await _harness.Client.GetFromJsonAsync<EpisodeDetailDto>(
            $"/api/alerting/episodes/{episodeId}/detail");
        Assert.NotNull(detail);
        return detail;
    }

    private async Task<Guid> SeedEpisodeAsync(
        string fingerprint, Guid? serviceId = null, Guid? applicationId = null, bool quieted = false)
    {
        var id = Guid.CreateVersion7();
        var service = serviceId ?? _harness.ServiceId;
        applicationId ??= _harness.ApplicationId;

        await _harness.ExecuteSqlAsync(
            $"""
            INSERT INTO alerting.episodes
                (id, opened_by_service_id, application_id, scope_key, watch, fingerprint, title,
                 recipe_version, fingerprint_rung, stack_truncated, alert_folded_into_storm,
                 opened_at, first_match_log_id, first_match_at, first_match_severity,
                 first_match_detail, error_count, warn_count, last_match_at, closed_at, close_reason)
            VALUES
                ('{id}', '{service}', '{applicationId}', 'app={applicationId}|env=prod', 1,
                 '{fingerprint}', '{fingerprint}', 1, 2, false, false, now(), 1,
                 now(), 17, 'boom', 1, 0, now(),
                 {(quieted ? "now()" : "NULL")}, {(quieted ? "1" : "NULL")});
            INSERT INTO alerting.participations
                (id, episode_id, service_id, version, first_at, last_at, error_count, warn_count)
            VALUES (gen_random_uuid(), '{id}', '{service}', NULL, now(), now(), 1, 0);
            """);
        return id;
    }
}
