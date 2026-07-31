using System.Net;
using System.Net.Http.Json;
using Bugler.Alerting.Episodes;
using Bugler.Alerting.ListEpisodes;

namespace Bugler.IntegrationTests;

public sealed class AlertingEpisodesApiTests : IAsyncLifetime
{
    private BuglerHarness _harness = null!;

    public async Task InitializeAsync() => _harness = await BuglerHarness.StartAsync();

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task Episodes_are_scoped_paged_and_newest_first()
    {
        var (foreignApp, foreignService, _) = await _harness.SeedApplicationAsync(
            "Crm", "acme", "prod", "backend");
        // Two episodes of one service: only the newest may be open (the one-open invariant).
        await SeedEpisodeAsync(_harness.ServiceId, _harness.ApplicationId, "own trouble", quieted: true);
        await SeedEpisodeAsync(_harness.ServiceId, _harness.ApplicationId, "more trouble");
        await SeedEpisodeAsync(foreignService, foreignApp, "foreign trouble");

        // The admin sees everything, newest first.
        var all = await _harness.Client.GetFromJsonAsync<ListEpisodesResponse>(
            "/api/alerting/episodes");
        Assert.Equal(3, all!.Items.Count);
        Assert.Equal("foreign trouble", all.Items[0].FirstLogBody);

        // Keyset paging: the second page starts past the first page's last row.
        var firstPage = await _harness.Client.GetFromJsonAsync<ListEpisodesResponse>(
            "/api/alerting/episodes?limit=2");
        var secondPage = await _harness.Client.GetFromJsonAsync<ListEpisodesResponse>(
            $"/api/alerting/episodes?limit=2&beforeId={firstPage!.Items[^1].Id}");
        Assert.Equal(2, firstPage.Items.Count);
        var remaining = Assert.Single(secondPage!.Items);
        Assert.Equal("own trouble", remaining.FirstLogBody);

        // A granted member sees only their application's episodes.
        var member = await _harness.CreateUserClientAsync(
            "member@bugler.test", "MemberPass123!", _harness.ApplicationId);
        var scoped = await member.GetFromJsonAsync<ListEpisodesResponse>("/api/alerting/episodes");
        Assert.Equal(2, scoped!.Items.Count);
        Assert.All(scoped.Items, e => Assert.Equal(_harness.ApplicationId, e.ApplicationId));

        // A user with no grants sees an empty list, not an error.
        var stranger = await _harness.CreateUserClientAsync("stranger@bugler.test", "Stranger123!");
        var nothing = await stranger.GetFromJsonAsync<ListEpisodesResponse>("/api/alerting/episodes");
        Assert.Empty(nothing!.Items);
    }

    [Fact]
    public async Task The_state_filter_and_the_recurrence_count_group_a_kind_of_trouble()
    {
        // The same kind of trouble three times over — quieted, quieted, open — plus a stranger.
        await SeedEpisodeAsync(_harness.ServiceId, _harness.ApplicationId, "flaky", quieted: true);
        await SeedEpisodeAsync(_harness.ServiceId, _harness.ApplicationId, "flaky", quieted: true);
        await SeedEpisodeAsync(_harness.ServiceId, _harness.ApplicationId, "flaky");
        await SeedEpisodeAsync(_harness.ServiceId, _harness.ApplicationId, "other trouble");

        var open = await _harness.Client.GetFromJsonAsync<ListEpisodesResponse>(
            "/api/alerting/episodes?state=Open");
        Assert.Equal(2, open!.Items.Count);
        Assert.All(open.Items, e => Assert.Equal(EpisodeState.Open, e.State));

        var quieted = await _harness.Client.GetFromJsonAsync<ListEpisodesResponse>(
            "/api/alerting/episodes?state=Quieted");
        Assert.Equal(2, quieted!.Items.Count);

        var both = await _harness.Client.GetFromJsonAsync<ListEpisodesResponse>(
            "/api/alerting/episodes?state=Open&state=Quieted");
        Assert.Equal(4, both!.Items.Count);

        // The newest "flaky" knows its two predecessors; the first knew none.
        var history = await _harness.Client.GetFromJsonAsync<ListEpisodesResponse>(
            $"/api/alerting/episodes?serviceId={_harness.ServiceId}&fingerprint=flaky");
        Assert.Equal(3, history!.Items.Count);
        Assert.Equal(2, history.Items[0].PriorCount);
        Assert.Equal(0, history.Items[^1].PriorCount);
    }

    [Fact]
    public async Task An_episode_is_acknowledged_taken_over_and_solved_by_hand()
    {
        await SeedEpisodeAsync(_harness.ServiceId, _harness.ApplicationId, "boom");
        var id = (await ListAsync()).Items[0].Id;
        var member = await _harness.CreateUserClientAsync(
            "member@bugler.test", "MemberPass123!", _harness.ApplicationId);

        // The admin takes it on; the member takes it over — one slot, last hand wins.
        Assert.Equal(HttpStatusCode.NoContent,
            (await _harness.Client.PostAsync($"/api/alerting/episodes/{id}/acknowledge", null)).StatusCode);
        Assert.Equal("Admin", (await ListAsync()).Items[0].AcknowledgedBy);
        Assert.Equal(HttpStatusCode.NoContent,
            (await member.PostAsync($"/api/alerting/episodes/{id}/acknowledge", null)).StatusCode);
        var takenOver = (await ListAsync()).Items[0];
        Assert.Equal("member@bugler.test", takenOver.AcknowledgedBy);
        Assert.Equal(EpisodeState.Open, takenOver.State);

        // Solving an open Episode closes it on the spot and consumes the acknowledgement.
        Assert.Equal(HttpStatusCode.NoContent,
            (await _harness.Client.PostAsync($"/api/alerting/episodes/{id}/solve", null)).StatusCode);
        var solved = (await ListAsync()).Items[0];
        Assert.Equal(EpisodeState.Solved, solved.State);
        Assert.Equal("Admin", solved.SolvedBy);
        Assert.NotNull(solved.ClosedAt);
        Assert.Null(solved.AcknowledgedBy);

        // The verdict is rendered once, and a Solved Episode is never Acknowledged.
        Assert.Equal(HttpStatusCode.Conflict,
            (await member.PostAsync($"/api/alerting/episodes/{id}/solve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await member.PostAsync($"/api/alerting/episodes/{id}/acknowledge", null)).StatusCode);
    }

    [Fact]
    public async Task An_acknowledgement_can_be_withdrawn_and_withdrawing_nothing_is_nothing()
    {
        await SeedEpisodeAsync(_harness.ServiceId, _harness.ApplicationId, "boom", quieted: true);
        var id = (await ListAsync()).Items[0].Id;

        // The mark survives quieting — it sits on a Quieted Episode until withdrawn.
        await _harness.Client.PostAsync($"/api/alerting/episodes/{id}/acknowledge", null);
        Assert.Equal("Admin", (await ListAsync()).Items[0].AcknowledgedBy);

        Assert.Equal(HttpStatusCode.NoContent,
            (await _harness.Client.DeleteAsync($"/api/alerting/episodes/{id}/acknowledgement")).StatusCode);
        Assert.Null((await ListAsync()).Items[0].AcknowledgedBy);

        Assert.Equal(HttpStatusCode.NoContent,
            (await _harness.Client.DeleteAsync($"/api/alerting/episodes/{id}/acknowledgement")).StatusCode);
    }

    [Fact]
    public async Task An_episode_outside_the_visibility_scope_is_nobodys_to_act_on()
    {
        await SeedEpisodeAsync(_harness.ServiceId, _harness.ApplicationId, "boom");
        var id = (await ListAsync()).Items[0].Id;

        var stranger = await _harness.CreateUserClientAsync("stranger@bugler.test", "Stranger123!");
        Assert.Equal(HttpStatusCode.NotFound,
            (await stranger.PostAsync($"/api/alerting/episodes/{id}/acknowledge", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await stranger.PostAsync($"/api/alerting/episodes/{id}/solve", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await stranger.DeleteAsync($"/api/alerting/episodes/{id}/acknowledgement")).StatusCode);

        // Nothing acted: the episode is untouched for those who do see it.
        var untouched = (await ListAsync()).Items[0];
        Assert.Equal(EpisodeState.Open, untouched.State);
        Assert.Null(untouched.AcknowledgedBy);
    }

    private async Task<ListEpisodesResponse> ListAsync() =>
        (await _harness.Client.GetFromJsonAsync<ListEpisodesResponse>("/api/alerting/episodes"))!;

    private Task SeedEpisodeAsync(
        Guid serviceId, Guid applicationId, string body, bool quieted = false) =>
        // Version-7 id from the client — the ordering the endpoint's keyset rides on. The body
        // doubles as the Fingerprint, as it does for template-less logs in detection.
        _harness.ExecuteSqlAsync(
            $"""
            INSERT INTO alerting.episodes
                (id, service_id, application_id, fingerprint, opened_at, first_log_id,
                 first_log_timestamp, first_log_severity, first_log_body, error_count,
                 warn_count, last_match_at, closed_at, close_reason)
            VALUES
                ('{Guid.CreateVersion7()}', '{serviceId}', '{applicationId}', '{body}', now(), 1,
                 now(), 17, '{body}', 1, 0, now(),
                 {(quieted ? "now()" : "NULL")}, {(quieted ? "1" : "NULL")})
            """);
}
