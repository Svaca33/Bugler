using System.Net.Http.Json;
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
        await SeedEpisodeAsync(_harness.ServiceId, _harness.ApplicationId, "own trouble", closed: true);
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

    private Task SeedEpisodeAsync(
        Guid serviceId, Guid applicationId, string body, bool closed = false) =>
        // Version-7 id from the client — the ordering the endpoint's keyset rides on.
        _harness.ExecuteSqlAsync(
            $"""
            INSERT INTO alerting.episodes
                (id, service_id, application_id, opened_at, first_log_id, first_log_timestamp,
                 first_log_severity, first_log_body, error_count, warn_count, last_match_at,
                 closed_at, close_reason)
            VALUES
                ('{Guid.CreateVersion7()}', '{serviceId}', '{applicationId}', now(), 1, now(), 17,
                 '{body}', 1, 0, now(),
                 {(closed ? "now()" : "NULL")}, {(closed ? "1" : "NULL")})
            """);
}
