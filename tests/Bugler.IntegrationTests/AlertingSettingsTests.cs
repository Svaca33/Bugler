using System.Net;
using System.Net.Http.Json;
using Bugler.Alerting.ManageAlertingSettings;

namespace Bugler.IntegrationTests;

public sealed class AlertingSettingsTests : IAsyncLifetime
{
    private BuglerHarness _harness = null!;

    public async Task InitializeAsync() => _harness = await BuglerHarness.StartAsync();

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task A_fresh_application_reports_no_settings_and_the_defaults()
    {
        var dto = await _harness.Client.GetFromJsonAsync<ApplicationAlertingDto>(
            $"/api/admin/applications/{_harness.ApplicationId}/alerting");

        Assert.NotNull(dto);
        Assert.Null(dto.Sensitivity);
        Assert.Null(dto.QuietWindowMinutes);
        Assert.Null(dto.ChatWebhook);
        Assert.Empty(dto.ServiceOverrides);
        Assert.Equal("Errors", dto.Defaults.Sensitivity.ToString());
        Assert.Equal(15, dto.Defaults.QuietWindowMinutes);
    }

    [Fact]
    public async Task Application_settings_and_a_service_override_round_trip()
    {
        var put = await _harness.Client.PutAsJsonAsync(
            $"/api/admin/applications/{_harness.ApplicationId}/alerting",
            new { sensitivity = "ErrorsAndWarnings", quietWindowMinutes = 30 });
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);

        var overridePut = await _harness.Client.PutAsJsonAsync(
            $"/api/admin/services/{_harness.ServiceId}/alerting",
            new { sensitivity = "Errors", quietWindowMinutes = (int?)null });
        Assert.Equal(HttpStatusCode.OK, overridePut.StatusCode);

        var dto = await _harness.Client.GetFromJsonAsync<ApplicationAlertingDto>(
            $"/api/admin/applications/{_harness.ApplicationId}/alerting");
        Assert.Equal("ErrorsAndWarnings", dto!.Sensitivity!.Value.ToString());
        Assert.Equal(30, dto.QuietWindowMinutes);
        var serviceOverride = Assert.Single(dto.ServiceOverrides);
        Assert.Equal(_harness.ServiceId, serviceOverride.ServiceId);
        Assert.Equal("Errors", serviceOverride.Sensitivity!.Value.ToString());
        Assert.Null(serviceOverride.QuietWindowMinutes);

        // Clearing every field deletes the override row: absent means inherit.
        var clear = await _harness.Client.PutAsJsonAsync(
            $"/api/admin/services/{_harness.ServiceId}/alerting",
            new { sensitivity = (string?)null, quietWindowMinutes = (int?)null });
        Assert.Equal(HttpStatusCode.OK, clear.StatusCode);
        await _harness.WaitForCountAsync("SELECT COUNT(*) FROM alerting.service_settings", 0);
    }

    [Fact]
    public async Task A_health_check_address_round_trips_and_is_probed_on_the_spot()
    {
        // Port 1 on the loopback answers nothing, so the trial probe reports a real failure
        // rather than needing a stub server — and the address is still saved, because a Service
        // that is down right now is exactly when somebody sets its health check up.
        var url = "http://127.0.0.1:1/health";
        var put = await _harness.Client.PutAsJsonAsync(
            $"/api/admin/services/{_harness.ServiceId}/alerting",
            new { sensitivity = (string?)null, quietWindowMinutes = (int?)null, healthCheckUrl = url });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var probed = await put.Content.ReadFromJsonAsync<SetServiceAlertingResponse>();
        Assert.NotNull(probed!.HealthCheck);
        Assert.False(probed.HealthCheck.Alive);
        Assert.Contains(url, probed.HealthCheck.Detail);

        var dto = await _harness.Client.GetFromJsonAsync<ApplicationAlertingDto>(
            $"/api/admin/applications/{_harness.ApplicationId}/alerting");
        Assert.Equal(url, Assert.Single(dto!.ServiceOverrides).HealthCheckUrl);

        // The address alone keeps the row alive; nothing else about the Service is overridden.
        var withoutUrl = await _harness.Client.PutAsJsonAsync(
            $"/api/admin/services/{_harness.ServiceId}/alerting",
            new { sensitivity = (string?)null, quietWindowMinutes = (int?)null, healthCheckUrl = (string?)null });
        Assert.Equal(HttpStatusCode.OK, withoutUrl.StatusCode);
        Assert.Null((await withoutUrl.Content.ReadFromJsonAsync<SetServiceAlertingResponse>())!.HealthCheck);
        await _harness.WaitForCountAsync("SELECT COUNT(*) FROM alerting.service_settings", 0);
    }

    [Fact]
    public async Task The_health_check_takes_http_but_not_nonsense()
    {
        // Deliberately looser than the Chat Webhook: a self-hosted Bugler usually shares a
        // network with what it watches, so plain http is the ordinary case.
        var plainHttp = await _harness.Client.PutAsJsonAsync(
            $"/api/admin/services/{_harness.ServiceId}/alerting",
            new { healthCheckUrl = "http://127.0.0.1:1/health" });
        Assert.Equal(HttpStatusCode.OK, plainHttp.StatusCode);

        foreach (var bad in new[] { "/health", "ftp://backend/health", "not a url" })
        {
            var response = await _harness.Client.PutAsJsonAsync(
                $"/api/admin/services/{_harness.ServiceId}/alerting",
                new { healthCheckUrl = bad });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task The_webhook_comes_back_as_its_host_and_never_in_full()
    {
        var put = await _harness.Client.PutAsJsonAsync(
            $"/api/admin/applications/{_harness.ApplicationId}/alerting/webhook",
            new { url = "https://chat.googleapis.com/v1/spaces/AAA/messages?key=SECRET&token=SECRET" });
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);

        var response = await _harness.Client.GetAsync(
            $"/api/admin/applications/{_harness.ApplicationId}/alerting");
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("chat.googleapis.com", body);
        Assert.DoesNotContain("SECRET", body);

        var insecure = await _harness.Client.PutAsJsonAsync(
            $"/api/admin/applications/{_harness.ApplicationId}/alerting/webhook",
            new { url = "http://chat.googleapis.com/v1/spaces/AAA" });
        Assert.Equal(HttpStatusCode.BadRequest, insecure.StatusCode);
    }

    [Fact]
    public async Task Guard_rails_hold_for_bad_input_and_unknown_targets()
    {
        var tooShort = await _harness.Client.PutAsJsonAsync(
            $"/api/admin/applications/{_harness.ApplicationId}/alerting",
            new { sensitivity = (string?)null, quietWindowMinutes = 0 });
        Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);

        var unknownService = await _harness.Client.PutAsJsonAsync(
            $"/api/admin/services/{Guid.NewGuid()}/alerting",
            new { sensitivity = "Off", quietWindowMinutes = (int?)null });
        Assert.Equal(HttpStatusCode.NotFound, unknownService.StatusCode);
    }

    [Fact]
    public async Task Only_an_admin_touches_detection_settings()
    {
        var member = await _harness.CreateUserClientAsync(
            "member@bugler.test", "MemberPass123!", _harness.ApplicationId);

        var get = await member.GetAsync($"/api/admin/applications/{_harness.ApplicationId}/alerting");
        Assert.Equal(HttpStatusCode.Forbidden, get.StatusCode);

        var put = await member.PutAsJsonAsync(
            $"/api/admin/applications/{_harness.ApplicationId}/alerting",
            new { sensitivity = "Off", quietWindowMinutes = (int?)null });
        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
    }

    [Fact]
    public async Task Turning_sensitivity_off_closes_the_open_episode_silently()
    {
        await _harness.ExecuteSqlAsync(
            $"""
            WITH opened AS (
                INSERT INTO alerting.episodes
                    (id, opened_by_service_id, application_id, scope_key, watch, fingerprint,
                     title, recipe_version, fingerprint_rung, stack_truncated,
                     alert_folded_into_storm, opened_at, first_match_log_id, first_match_at,
                     first_match_severity, first_match_detail, error_count, warn_count,
                     last_match_at)
                VALUES
                    (gen_random_uuid(), '{_harness.ServiceId}', '{_harness.ApplicationId}',
                     'app={_harness.ApplicationId}|env=prod', 1, 'boom', 'boom', 1, 2, false,
                     false, now(), 1, now(), 17, 'boom', 3, 0, now())
                RETURNING id)
            INSERT INTO alerting.participations
                (id, episode_id, service_id, version, first_at, last_at, error_count, warn_count)
            SELECT gen_random_uuid(), id, '{_harness.ServiceId}', NULL, now(), now(), 3, 0
            FROM opened
            """);

        var put = await _harness.Client.PutAsJsonAsync(
            $"/api/admin/applications/{_harness.ApplicationId}/alerting",
            new { sensitivity = "Off", quietWindowMinutes = (int?)null });
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);

        // close_reason 2 = WatchOff; no All Clear delivery may appear.
        await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.episodes WHERE closed_at IS NOT NULL AND close_reason = 2", 1);
        var deliveries = await _harness.WaitForCountAsync("SELECT COUNT(*) FROM alerting.deliveries", 0);
        Assert.Equal(0, deliveries);
    }
}
