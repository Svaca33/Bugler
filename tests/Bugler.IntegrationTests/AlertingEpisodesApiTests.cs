using System.Net;
using System.Net.Http.Json;
using Bugler.Alerting.DescribeEpisode;
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
        Assert.Equal("foreign trouble", all.Items[0].FirstMatchDetail);

        // Keyset paging: the second page starts past the first page's last row.
        var firstPage = await _harness.Client.GetFromJsonAsync<ListEpisodesResponse>(
            "/api/alerting/episodes?limit=2");
        var secondPage = await _harness.Client.GetFromJsonAsync<ListEpisodesResponse>(
            $"/api/alerting/episodes?limit=2&beforeId={firstPage!.Items[^1].Id}");
        Assert.Equal(2, firstPage.Items.Count);
        var remaining = Assert.Single(secondPage!.Items);
        Assert.Equal("own trouble", remaining.FirstMatchDetail);

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
    public async Task The_grouped_list_shows_each_kind_of_trouble_as_its_latest_episode()
    {
        // "flaky" three times over (the newest open), "other trouble" once, quieted.
        await SeedEpisodeAsync(_harness.ServiceId, _harness.ApplicationId, "flaky", quieted: true);
        await SeedEpisodeAsync(_harness.ServiceId, _harness.ApplicationId, "flaky", quieted: true);
        await SeedEpisodeAsync(_harness.ServiceId, _harness.ApplicationId, "flaky");
        await SeedEpisodeAsync(_harness.ServiceId, _harness.ApplicationId, "other trouble", quieted: true);

        // One face per kind, newest first, each knowing its predecessors.
        var faces = await _harness.Client.GetFromJsonAsync<ListEpisodesResponse>(
            "/api/alerting/episodes?latestPerFingerprint=true");
        Assert.Equal(2, faces!.Items.Count);
        Assert.Equal("other trouble", faces.Items[0].FirstMatchDetail);
        Assert.Equal("flaky", faces.Items[1].FirstMatchDetail);
        Assert.Equal(2, faces.Items[1].PriorCount);

        // The state filter judges the face: the flaky group has quieted episodes, but its face
        // is open, so only "other trouble" answers to Quieted.
        var quieted = await _harness.Client.GetFromJsonAsync<ListEpisodesResponse>(
            "/api/alerting/episodes?latestPerFingerprint=true&state=Quieted");
        var quietFace = Assert.Single(quieted!.Items);
        Assert.Equal("other trouble", quietFace.FirstMatchDetail);

        // Keyset paging walks faces, skipping the history between them.
        var firstPage = await _harness.Client.GetFromJsonAsync<ListEpisodesResponse>(
            "/api/alerting/episodes?latestPerFingerprint=true&limit=1");
        var secondPage = await _harness.Client.GetFromJsonAsync<ListEpisodesResponse>(
            $"/api/alerting/episodes?latestPerFingerprint=true&limit=1&beforeId={firstPage!.Items[0].Id}");
        Assert.Equal("flaky", Assert.Single(secondPage!.Items).FirstMatchDetail);

        // Counts follow the faces (1 open + 1 quieted), not the episodes (1 + 3).
        var grouped = await _harness.Client.GetFromJsonAsync<EpisodeCountsResponse>(
            "/api/alerting/episodes/counts?latestPerFingerprint=true");
        Assert.Equal(new EpisodeCountsResponse(1, 1, 0, 0, 0, 0), grouped);
        var flat = await _harness.Client.GetFromJsonAsync<EpisodeCountsResponse>(
            "/api/alerting/episodes/counts");
        Assert.Equal(new EpisodeCountsResponse(1, 3, 0, 0, 0, 0), flat);
    }

    [Fact]
    public async Task Two_episodes_differing_only_in_their_watch_are_two_kinds_of_trouble()
    {
        // One Episode Scope, one Fingerprint string, two Watches. A Fingerprint means something
        // different under each (Alerting ADR 0007), so a log whose body happens to read like the
        // Health Check Watch's kind is other trouble: neither Episode is the other's history, neither
        // overtakes the other, and a verdict on one leaves the other's mark standing.
        var logs = NextId();
        await Seed(logs, _harness.ServiceId, _harness.ApplicationId, "flaky", quieted: false);
        var health = NextId();
        await Seed(
            health, _harness.ServiceId, _harness.ApplicationId, "flaky", quieted: false, watch: 2);

        var both = await ListAsync();
        Assert.Equal(2, both.Items.Count);
        Assert.All(both.Items, e => Assert.Equal(0, e.PriorCount));

        // Two kinds, so two faces — the grouped list groups by the kind, not by the string.
        var faces = await _harness.Client.GetFromJsonAsync<ListEpisodesResponse>(
            "/api/alerting/episodes?latestPerFingerprint=true");
        Assert.Equal(2, faces!.Items.Count);

        // The older is still the newest of its own kind, so a hand lands on it rather than 409ing.
        Assert.Equal(HttpStatusCode.NoContent,
            (await _harness.Client.PostAsync($"/api/alerting/episodes/{logs}/acknowledge", null))
                .StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,
            (await _harness.Client.PostAsync($"/api/alerting/episodes/{health}/acknowledge", null))
                .StatusCode);

        // Neither wears the other's mark as its kind's earlier acknowledgement.
        var marked = await ListAsync();
        Assert.All(marked.Items, e => Assert.Null(e.EarlierAcknowledgedBy));

        // Solve consumes the acknowledgements of its own kind alone.
        Assert.Equal(HttpStatusCode.NoContent,
            (await _harness.Client.PostAsync($"/api/alerting/episodes/{logs}/solve", null))
                .StatusCode);
        var afterwards = await ListAsync();
        Assert.Null(afterwards.Items.Single(e => e.Id == logs).AcknowledgedBy);
        Assert.Equal("Admin", afterwards.Items.Single(e => e.Id == health).AcknowledgedBy);

        // And the detail reads the kind the same way the list does.
        var detail = await _harness.Client.GetFromJsonAsync<EpisodeDetailDto>(
            $"/api/alerting/episodes/{health}/detail");
        Assert.Equal(0, detail!.Episode.PriorCount);
        Assert.Equal(EpisodeState.Open, detail.Episode.State);
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
    public async Task Hands_land_only_on_the_newest_episode_and_solve_wipes_the_kinds_acknowledgements()
    {
        await SeedEpisodeAsync(_harness.ServiceId, _harness.ApplicationId, "flaky", quieted: true);
        var older = (await ListAsync()).Items[0].Id;

        // The mark lands on the newest of its kind — currently the quieted one, mark alone.
        Assert.Equal(HttpStatusCode.NoContent,
            (await _harness.Client.PostAsync($"/api/alerting/episodes/{older}/acknowledge", null)).StatusCode);

        // The trouble returns: a newer episode arrives and the older one becomes history.
        await SeedEpisodeAsync(_harness.ServiceId, _harness.ApplicationId, "flaky");
        var newer = (await ListAsync()).Items[0].Id;
        Assert.NotEqual(older, newer);

        // History takes no hands: not the mark, not its withdrawal, not the verdict.
        Assert.Equal(HttpStatusCode.Conflict,
            (await _harness.Client.PostAsync($"/api/alerting/episodes/{older}/acknowledge", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await _harness.Client.DeleteAsync($"/api/alerting/episodes/{older}/acknowledgement")).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await _harness.Client.PostAsync($"/api/alerting/episodes/{older}/solve", null)).StatusCode);

        // The newest carries the frozen mark as context while being unacknowledged itself.
        var face = (await ListAsync()).Items[0];
        Assert.Null(face.AcknowledgedBy);
        Assert.Equal("Admin", face.EarlierAcknowledgedBy);
        Assert.NotNull(face.EarlierAcknowledgedAt);

        // Solve consumes every acknowledgement the kind holds — the older, frozen mark included.
        Assert.Equal(HttpStatusCode.NoContent,
            (await _harness.Client.PostAsync($"/api/alerting/episodes/{newer}/solve", null)).StatusCode);
        var afterwards = await ListAsync();
        Assert.All(afterwards.Items, e => Assert.Null(e.AcknowledgedBy));
        Assert.Null(afterwards.Items[0].EarlierAcknowledgedBy);

        // The stripped mark left its trace (ADR 0006): the older episode's Journal records the
        // withdrawal by the solver's hand, at the verdict's moment — no mark vanishes untold.
        var olderDetail = await _harness.Client.GetFromJsonAsync<EpisodeDetailDto>(
            $"/api/alerting/episodes/{older}/detail");
        Assert.Equal(
            [(JournalEntryKind.Acknowledged, "Admin"), (JournalEntryKind.Withdrawn, "Admin")],
            olderDetail!.Journal.Select(j => (j.Kind, j.By)));
        var newerDetail = await _harness.Client.GetFromJsonAsync<EpisodeDetailDto>(
            $"/api/alerting/episodes/{newer}/detail");
        var verdict = Assert.Single(newerDetail!.Journal);
        Assert.Equal(JournalEntryKind.Solved, verdict.Kind);
        Assert.Equal(verdict.At, olderDetail.Journal[^1].At);
    }

    [Fact]
    public async Task The_journal_keeps_every_hand_and_only_acts_are_written()
    {
        await SeedEpisodeAsync(_harness.ServiceId, _harness.ApplicationId, "boom");
        var id = (await ListAsync()).Items[0].Id;
        var member = await _harness.CreateUserClientAsync(
            "member@bugler.test", "MemberPass123!", _harness.ApplicationId);

        // Acknowledge, re-acknowledge (not an act), take-over, a colleague withdrawing the
        // mark, withdrawing nothing (not an act), acknowledge again, and the verdict.
        await _harness.Client.PostAsync($"/api/alerting/episodes/{id}/acknowledge", null);
        var firstMarkAt = (await ListAsync()).Items[0].AcknowledgedAt;
        await _harness.Client.PostAsync($"/api/alerting/episodes/{id}/acknowledge", null);
        // Not an act on either side: the mark kept its first moment, the Journal stays quiet.
        Assert.Equal(firstMarkAt, (await ListAsync()).Items[0].AcknowledgedAt);
        await member.PostAsync($"/api/alerting/episodes/{id}/acknowledge", null);
        await _harness.Client.DeleteAsync($"/api/alerting/episodes/{id}/acknowledgement");
        await _harness.Client.DeleteAsync($"/api/alerting/episodes/{id}/acknowledgement");
        await member.PostAsync($"/api/alerting/episodes/{id}/acknowledge", null);
        await _harness.Client.PostAsync($"/api/alerting/episodes/{id}/solve", null);

        var detail = await _harness.Client.GetFromJsonAsync<EpisodeDetailDto>(
            $"/api/alerting/episodes/{id}/detail");
        Assert.Equal(
            [
                (JournalEntryKind.Acknowledged, "Admin"),
                (JournalEntryKind.Acknowledged, "member@bugler.test"),
                (JournalEntryKind.Withdrawn, "Admin"),
                (JournalEntryKind.Acknowledged, "member@bugler.test"),
                (JournalEntryKind.Solved, "Admin"),
            ],
            detail!.Journal.Select(j => (j.Kind, j.By)));
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
        // doubles as the Fingerprint, as it does for template-less logs in detection. The Scope
        // carries the Service, which is what these tests were written against and what an
        // Application that scopes by Service Name still gets (ADR 0034).
        Seed(NextId(), serviceId, applicationId, body, quieted);

    private Task Seed(
        Guid id, Guid serviceId, Guid applicationId, string body, bool quieted,
        short watch = 1) =>
        _harness.ExecuteSqlAsync(
            $"""
            INSERT INTO alerting.episodes
                (id, opened_by_service_id, application_id, scope_key, watch, fingerprint, title,
                 recipe_version, fingerprint_rung, stack_truncated, alert_folded_into_storm,
                 opened_at, first_match_log_id, first_match_at, first_match_severity,
                 first_match_detail, error_count, warn_count, last_match_at, closed_at, close_reason)
            VALUES
                ('{id}', '{serviceId}', '{applicationId}',
                 'app={applicationId}|env=prod|name={serviceId}', {watch}, '{body}', '{body}',
                 1, 2, false, false, now(),
                 {(watch == 1 ? "1" : "NULL")}, now(), {(watch == 1 ? "17" : "NULL")},
                 '{body}', 1, 0, now(),
                 {(quieted ? "now()" : "NULL")}, {(quieted ? "1" : "NULL")});
            INSERT INTO alerting.participations
                (id, episode_id, service_id, version, first_at, last_at, error_count, warn_count)
            VALUES (gen_random_uuid(), '{id}', '{serviceId}', NULL, now(), now(), 1, 0);
            """);

    /// <summary>
    /// Seeded ids, spaced a second apart. A version-7 id carries a millisecond and random bits
    /// under it, so two rows written inside the same millisecond order at random — which is a fair
    /// answer from the endpoint and a coin toss for a test asserting which of them is the newest.
    /// Four inserts are far enough apart on a laptop and not on a hosted runner, which is where
    /// this first flipped.
    /// </summary>
    private Guid NextId() => Guid.CreateVersion7(_idBase.AddSeconds(_seeded++));

    private readonly DateTimeOffset _idBase = DateTimeOffset.UtcNow;
    private int _seeded;
}
