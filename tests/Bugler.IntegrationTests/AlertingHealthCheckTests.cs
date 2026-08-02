using System.Net;
using System.Net.Http.Json;
using Bugler.Alerting.CloseQuietEpisodes;
using Bugler.Alerting.WatchHealthChecks;

namespace Bugler.IntegrationTests;

/// <summary>
/// The Health Check Watch end to end: an address nobody answers opens one Episode after three
/// failures, further failures feed it rather than multiplying it, and clearing the address Mutes
/// it. Port 1 on the loopback is the stand-in for a dead Service — nothing listens there, so the
/// probe fails the way a crashed process makes it fail.
/// </summary>
public sealed class AlertingHealthCheckTests : IAsyncLifetime
{
    private const string DeadAddress = "http://127.0.0.1:1/health";

    private BuglerHarness _harness = null!;
    private HealthCheckWatcher _watcher = null!;
    private EpisodeCloser _closer = null!;

    public async Task InitializeAsync()
    {
        _harness = await BuglerHarness.StartAsync();
        _watcher = _harness.GetRequiredService<HealthCheckWatcher>();
        _closer = _harness.GetRequiredService<EpisodeCloser>();
    }

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task Three_failures_open_one_episode_and_later_ones_only_feed_it()
    {
        await SetHealthCheckAsync(DeadAddress);

        await _watcher.WatchOnceAsync(default);
        await _watcher.WatchOnceAsync(default);
        // Two failures are not yet worth waking anybody.
        await _harness.WaitForCountAsync("SELECT COUNT(*) FROM alerting.episodes", 0);

        await _watcher.WatchOnceAsync(default);
        await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.episodes WHERE watch = 2 "
            + "AND fingerprint = '(health check failing)' AND closed_at IS NULL "
            + "AND first_match_log_id IS NULL AND first_match_severity IS NULL", 1);

        // The evidence names the address, so the Alert explains itself without reading settings.
        await _harness.WaitForCountAsync(
            $"SELECT COUNT(*) FROM alerting.episodes WHERE first_match_detail LIKE '%{DeadAddress}%'", 1);

        // Every further failure is a match on the same Episode — never a second one, never a
        // second Alert.
        await _watcher.WatchOnceAsync(default);
        await _watcher.WatchOnceAsync(default);
        await _harness.WaitForCountAsync("SELECT COUNT(*) FROM alerting.episodes", 1);
    }

    [Fact]
    public async Task Clearing_the_address_mutes_the_episode_rather_than_resolving_it()
    {
        await SetHealthCheckAsync(DeadAddress);
        for (var probe = 0; probe < 3; probe++)
        {
            await _watcher.WatchOnceAsync(default);
        }

        await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.episodes WHERE closed_at IS NULL", 1);

        await SetHealthCheckAsync(null);

        // close_reason 2 = WatchOff: the watching stopped, nothing is claimed about the Service.
        await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.episodes WHERE closed_at IS NOT NULL AND close_reason = 2", 1);
    }

    [Fact]
    public async Task Turning_sensitivity_off_leaves_the_health_check_episode_alone()
    {
        // Sensitivity is the Logs Watch's switch and says nothing about this one.
        await SetHealthCheckAsync(DeadAddress);
        for (var probe = 0; probe < 3; probe++)
        {
            await _watcher.WatchOnceAsync(default);
        }

        var off = await _harness.Client.PutAsJsonAsync(
            $"/api/admin/services/{_harness.ServiceId}/alerting",
            new { sensitivity = "Off", healthCheckUrl = DeadAddress });
        Assert.Equal(HttpStatusCode.OK, off.StatusCode);

        await _closer.CloseOnceAsync(default);
        await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.episodes WHERE watch = 2 AND closed_at IS NULL", 1);
    }

    private async Task SetHealthCheckAsync(string? url)
    {
        var response = await _harness.Client.PutAsJsonAsync(
            $"/api/admin/services/{_harness.ServiceId}/alerting",
            new { sensitivity = (string?)null, quietWindowMinutes = (int?)null, healthCheckUrl = url });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
