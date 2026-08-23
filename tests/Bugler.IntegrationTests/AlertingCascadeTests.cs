using System.Net;
using System.Net.Http.Json;

namespace Bugler.IntegrationTests;

public sealed class AlertingCascadeTests : IAsyncLifetime
{
    private BuglerHarness _harness = null!;

    public async Task InitializeAsync() => _harness = await BuglerHarness.StartAsync();

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task Deleting_a_service_drops_its_episodes_override_and_subscriptions()
    {
        await _harness.Client.PutAsJsonAsync(
            $"/api/admin/services/{_harness.ServiceId}/alerting",
            new { sensitivity = "ErrorsAndWarnings", quietWindowMinutes = (int?)null });
        await _harness.Client.PutAsJsonAsync("/api/alerting/subscriptions", new
        {
            applicationIds = Array.Empty<Guid>(),
            serviceIds = new[] { _harness.ServiceId },
        });
        await SeedEpisodeWithDeliveryAsync();

        var delete = await _harness.Client.DeleteAsync($"/api/admin/services/{_harness.ServiceId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        Assert.Equal(0, await _harness.WaitForCountAsync("SELECT COUNT(*) FROM alerting.episodes", 0));
        Assert.Equal(0, await _harness.WaitForCountAsync("SELECT COUNT(*) FROM alerting.deliveries", 0));
        Assert.Equal(0, await _harness.WaitForCountAsync("SELECT COUNT(*) FROM alerting.service_settings", 0));
        Assert.Equal(0, await _harness.WaitForCountAsync("SELECT COUNT(*) FROM alerting.subscriptions", 0));
    }

    [Fact]
    public async Task Deleting_an_application_drops_its_settings_and_subscriptions()
    {
        await _harness.Client.PutAsJsonAsync(
            $"/api/admin/applications/{_harness.ApplicationId}/alerting",
            new { sensitivity = "ErrorsAndWarnings", quietWindowMinutes = 30 });
        await _harness.Client.PutAsJsonAsync("/api/alerting/subscriptions", new
        {
            applicationIds = new[] { _harness.ApplicationId },
            serviceIds = Array.Empty<Guid>(),
        });

        var delete = await _harness.Client.DeleteAsync(
            $"/api/admin/applications/{_harness.ApplicationId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        Assert.Equal(0, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.application_settings", 0));
        Assert.Equal(0, await _harness.WaitForCountAsync("SELECT COUNT(*) FROM alerting.subscriptions", 0));
        Assert.Equal(0, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM registry.outbox_messages", 0));
    }

    [Fact]
    public async Task Deleting_a_user_drops_their_subscriptions_and_lapses_their_pending_mail()
    {
        var member = await _harness.CreateUserClientAsync(
            "member@bugler.test", "MemberPass123!", _harness.ApplicationId);
        await member.PutAsJsonAsync("/api/alerting/subscriptions", new
        {
            applicationIds = new[] { _harness.ApplicationId },
            serviceIds = Array.Empty<Guid>(),
        });

        var userId = await _harness.FindUserIdAsync("member@bugler.test");
        await SeedEpisodeWithDeliveryAsync(userId);

        var delete = await _harness.Client.DeleteAsync($"/api/users/{userId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        Assert.Equal(0, await _harness.WaitForCountAsync("SELECT COUNT(*) FROM alerting.subscriptions", 0));
        Assert.Equal(1, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.deliveries WHERE lapsed_at IS NOT NULL", 1));
        Assert.Equal(0, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM access.outbox_messages", 0));
    }

    /// <summary>One open episode of the seeded Service, with one pending mail Delivery.</summary>
    private Task SeedEpisodeWithDeliveryAsync(Guid? userId = null) =>
        SeedEpisodeAsync([_harness.ServiceId], userId);

    /// <summary>
    /// One open Episode fed by every named Service — the shape ADR 0034 introduced: bound by a
    /// Scope, opened by the first of them, and holding a Participation for each.
    /// </summary>
    private async Task<Guid> SeedEpisodeAsync(IReadOnlyList<Guid> serviceIds, Guid? userId = null)
    {
        var episodeId = Guid.NewGuid();
        var recipient = userId ?? Guid.NewGuid();
        var participations = string.Join(",\n", serviceIds.Select(id =>
            $"(gen_random_uuid(), '{episodeId}', '{id}', NULL, now(), now(), 1, 0)"));
        await _harness.ExecuteSqlAsync(
            $"""
            INSERT INTO alerting.episodes
                (id, opened_by_service_id, application_id, scope_key, watch, fingerprint, title,
                 recipe_version, fingerprint_rung, stack_truncated, alert_folded_into_storm,
                 opened_at, first_match_log_id, first_match_at, first_match_severity,
                 first_match_detail, error_count, warn_count, last_match_at)
            VALUES
                ('{episodeId}', '{serviceIds[0]}', '{_harness.ApplicationId}',
                 'app={_harness.ApplicationId}|env=prod', 1, 'boom', 'boom',
                 1, 2, false, false, now(), 1, now(), 17, 'boom', 1, 0, now());
            INSERT INTO alerting.participations
                (id, episode_id, service_id, version, first_at, last_at, error_count, warn_count)
            VALUES
                {participations};
            INSERT INTO alerting.deliveries
                (id, episode_id, kind, channel, user_id, attempts, created_at, next_attempt_at)
            VALUES
                (gen_random_uuid(), '{episodeId}', 1, 1, '{recipient}', 0, now(), now());
            """);
        return episodeId;
    }

    [Fact]
    public async Task Deleting_one_of_two_participating_services_leaves_the_episode_standing()
    {
        var (second, _) = await _harness.SeedServiceAsync(
            _harness.ApplicationId, "acme", "prod", "worker");
        await SeedEpisodeAsync([_harness.ServiceId, second]);

        var delete = await _harness.Client.DeleteAsync($"/api/admin/services/{second}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        // The Episode is no longer any one Service's (ADR 0034): somebody may still see it, so
        // it stands — with one Participation fewer and its opening evidence still named.
        Assert.Equal(1, await _harness.WaitForCountAsync("SELECT COUNT(*) FROM alerting.episodes", 1));
        Assert.Equal(1, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.participations", 1));
        Assert.Equal(1, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.episodes WHERE opened_by_service_id IS NOT NULL", 1));
    }

    [Fact]
    public async Task Deleting_the_last_participating_service_takes_the_episode_with_it()
    {
        var (second, _) = await _harness.SeedServiceAsync(
            _harness.ApplicationId, "acme", "prod", "worker");
        // Opened by the second, so the first Deletion also has to null the opening evidence out.
        await SeedEpisodeAsync([second, _harness.ServiceId]);

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await _harness.Client.DeleteAsync($"/api/admin/services/{second}")).StatusCode);
        Assert.Equal(1, await _harness.WaitForCountAsync("SELECT COUNT(*) FROM alerting.episodes", 1));
        Assert.Equal(1, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.episodes WHERE opened_by_service_id IS NULL", 1));

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await _harness.Client.DeleteAsync($"/api/admin/services/{_harness.ServiceId}")).StatusCode);

        // Nobody may see anything in it any more, so it goes — and its Deliveries with it.
        Assert.Equal(0, await _harness.WaitForCountAsync("SELECT COUNT(*) FROM alerting.episodes", 0));
        Assert.Equal(0, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.participations", 0));
        Assert.Equal(0, await _harness.WaitForCountAsync("SELECT COUNT(*) FROM alerting.deliveries", 0));
    }
}
