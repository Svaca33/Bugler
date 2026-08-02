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
    private async Task SeedEpisodeWithDeliveryAsync(Guid? userId = null)
    {
        var episodeId = Guid.NewGuid();
        var recipient = userId ?? Guid.NewGuid();
        await _harness.ExecuteSqlAsync(
            $"""
            INSERT INTO alerting.episodes
                (id, service_id, application_id, watch, fingerprint, opened_at, first_match_log_id,
                 first_match_at, first_match_severity, first_match_detail, error_count,
                 warn_count, last_match_at)
            VALUES
                ('{episodeId}', '{_harness.ServiceId}', '{_harness.ApplicationId}', 1, 'boom',
                 now(), 1, now(), 17, 'boom', 1, 0, now());
            INSERT INTO alerting.deliveries
                (id, episode_id, kind, channel, user_id, attempts, created_at, next_attempt_at)
            VALUES
                (gen_random_uuid(), '{episodeId}', 1, 1, '{recipient}', 0, now(), now());
            """);
    }
}
