using System.Net.Http.Json;
using Bugler.Alerting.CloseQuietEpisodes;
using Bugler.Alerting.DeliverMessages;
using Bugler.Alerting.DetectEpisodes;
using Bugler.Mail;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Bugler.IntegrationTests;

public sealed class AlertingDeliveryTests : IAsyncLifetime
{
    private BuglerHarness _harness = null!;
    private EpisodeDetector _detector = null!;
    private EpisodeCloser _closer = null!;
    private DeliveryRunner _runner = null!;
    private readonly RecordingMailSender _mail = new();
    private readonly RecordingChatSender _chat = new();

    public async Task InitializeAsync()
    {
        _harness = await BuglerHarness.StartAsync(
            builder =>
            {
                builder.UseSetting("Mail:Smtp:Host", "smtp.test");
                builder.UseSetting("Mail:Smtp:From", "bugler@test.local");
                builder.ConfigureTestServices(services =>
                {
                    services.AddSingleton<IMailSender>(_mail);
                    services.AddSingleton<IChatSender>(_chat);
                });
            },
            publicBaseUrl: "https://bugler.test");
        _detector = _harness.GetRequiredService<EpisodeDetector>();
        _closer = _harness.GetRequiredService<EpisodeCloser>();
        _runner = _harness.GetRequiredService<DeliveryRunner>();
    }

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task An_episode_is_announced_by_mail_and_chat_and_quiets_in_silence()
    {
        await SubscribeAdminAndSetWebhookAsync();
        await OpenEpisodeAsync();

        await _runner.DeliverOnceAsync(CancellationToken.None);

        var alertMail = Assert.Single(_mail.Sent);
        Assert.Equal(BuglerHarness.AdminEmail, alertMail.ToEmail);
        Assert.Equal("[Bugler] Trouble in Eshop acme/prod/web", alertMail.Subject);
        Assert.Contains("boom", alertMail.TextBody);
        Assert.Contains("/episodes?episode=", alertMail.TextBody);
        Assert.NotNull(alertMail.HtmlBody);
        Assert.Contains("boom", alertMail.HtmlBody);
        var alertChat = Assert.Single(_chat.Sent);
        Assert.Contains("chat.googleapis.com", alertChat.WebhookUrl);
        Assert.Contains("boom", alertChat.Alert.MatchDetail);

        // The service falls quiet: backdate the last match past the 15-minute default window.
        // The Episode becomes Quieted — and nobody is told (ADR 0003): the Alert is the only
        // message Bugler sends.
        await _harness.ExecuteSqlAsync(
            "UPDATE alerting.episodes SET last_match_at = now() - interval '20 minutes'");
        await _closer.CloseOnceAsync(CancellationToken.None);
        await _runner.DeliverOnceAsync(CancellationToken.None);

        Assert.Single(_mail.Sent);
        Assert.Single(_chat.Sent);
        Assert.Equal(1, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.episodes WHERE closed_at IS NOT NULL AND close_reason = 1", 1));
        Assert.Equal(0, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.deliveries WHERE kind = 2", 0));
    }

    [Fact]
    public async Task A_recipient_without_access_waits_dormant_and_a_stale_delivery_lapses()
    {
        var member = await _harness.CreateUserClientAsync(
            "member@bugler.test", "MemberPass123!", _harness.ApplicationId);
        await member.PutAsJsonAsync("/api/alerting/subscriptions", new
        {
            applicationIds = new[] { _harness.ApplicationId },
            serviceIds = Array.Empty<Guid>(),
        });
        await OpenEpisodeAsync();

        // The grant is revoked before delivery: nothing may leave, the row stays dormant.
        var userId = await _harness.FindUserIdAsync("member@bugler.test");
        var revoke = await _harness.Client.DeleteAsync(
            $"/api/users/{userId}/grants/{_harness.ApplicationId}");
        revoke.EnsureSuccessStatusCode();
        await _runner.DeliverOnceAsync(CancellationToken.None);
        Assert.Empty(_mail.Sent);
        Assert.Equal(1, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.deliveries WHERE delivered_at IS NULL "
            + "AND lapsed_at IS NULL AND attempts = 0", 1));

        // Re-granted within the TTL: the dormant row wakes and delivers.
        var grant = await _harness.Client.PostAsJsonAsync(
            $"/api/users/{userId}/grants", new { applicationId = _harness.ApplicationId });
        grant.EnsureSuccessStatusCode();
        await _runner.DeliverOnceAsync(CancellationToken.None);
        Assert.Equal("member@bugler.test", Assert.Single(_mail.Sent).ToEmail);

        // A delivery that outlives its TTL is worth nothing — it lapses instead of arriving.
        await _harness.ExecuteSqlAsync(
            "UPDATE alerting.deliveries SET delivered_at = NULL, created_at = now() - interval '7 hours'");
        await _runner.DeliverOnceAsync(CancellationToken.None);
        Assert.Equal(1, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.deliveries WHERE lapsed_at IS NOT NULL", 1));
    }

    [Fact]
    public async Task A_removed_webhook_lapses_the_chat_delivery()
    {
        await SubscribeAdminAndSetWebhookAsync();
        await OpenEpisodeAsync();
        var clear = await _harness.Client.PutAsJsonAsync(
            $"/api/admin/applications/{_harness.ApplicationId}/alerting/webhook",
            new { url = (string?)null });
        clear.EnsureSuccessStatusCode();

        await _runner.DeliverOnceAsync(CancellationToken.None);

        Assert.Empty(_chat.Sent);
        Assert.Single(_mail.Sent);
        Assert.Equal(1, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.deliveries WHERE channel = 2 AND lapsed_at IS NOT NULL", 1));
    }

    private async Task SubscribeAdminAndSetWebhookAsync()
    {
        var subscribe = await _harness.Client.PutAsJsonAsync("/api/alerting/subscriptions", new
        {
            applicationIds = new[] { _harness.ApplicationId },
            serviceIds = Array.Empty<Guid>(),
        });
        subscribe.EnsureSuccessStatusCode();
        var webhook = await _harness.Client.PutAsJsonAsync(
            $"/api/admin/applications/{_harness.ApplicationId}/alerting/webhook",
            new { url = "https://chat.googleapis.com/v1/spaces/AAA/messages" });
        webhook.EnsureSuccessStatusCode();
    }

    /// <summary>Seeds the cursor, inserts one error log, and lets detection open the Episode.</summary>
    [Fact]
    public async Task Following_both_an_application_and_its_service_is_told_once()
    {
        // One Alert per Episode per recipient (ADR 0034), never one per subscription.
        await _harness.Client.PutAsJsonAsync("/api/alerting/subscriptions", new
        {
            applicationIds = new[] { _harness.ApplicationId },
            serviceIds = new[] { _harness.ServiceId },
        });
        await OpenEpisodeAsync();

        await _runner.DeliverOnceAsync(CancellationToken.None);

        Assert.Single(_mail.Sent);
    }

    [Fact]
    public async Task A_service_falling_into_a_running_episode_tells_its_own_followers_since_when()
    {
        var (tenant, _) = await _harness.SeedServiceAsync(
            _harness.ApplicationId, "globex", "prod", "web");
        var member = await _harness.CreateUserClientAsync(
            "tenant@bugler.test", "TenantPass123!", _harness.ApplicationId);
        // This person follows their own tenant alone — the very case the joining Alert is for.
        await member.PutAsJsonAsync("/api/alerting/subscriptions", new
        {
            applicationIds = Array.Empty<Guid>(),
            serviceIds = new[] { tenant },
        });

        await OpenEpisodeAsync();
        await _runner.DeliverOnceAsync(CancellationToken.None);
        Assert.Empty(_mail.Sent); // Their tenant is not in it yet.

        await LogBoomAsync(tenant);
        await _detector.DetectOnceAsync(CancellationToken.None);
        await _runner.DeliverOnceAsync(CancellationToken.None);

        var joined = Assert.Single(_mail.Sent);
        Assert.Equal("tenant@bugler.test", joined.ToEmail);
        Assert.Equal("[Bugler] Trouble reached Eshop globex/prod/web", joined.Subject);
        Assert.Contains("running since", joined.TextBody);
        Assert.DoesNotContain("started logging trouble", joined.TextBody);

        // One message per recipient per Episode: their tenant logging more owes nothing further.
        await LogBoomAsync(tenant);
        await _detector.DetectOnceAsync(CancellationToken.None);
        await _runner.DeliverOnceAsync(CancellationToken.None);
        Assert.Single(_mail.Sent);
    }

    [Fact]
    public async Task A_storm_folds_the_alerts_it_would_have_sent_into_one_digest()
    {
        await SubscribeAdminAndSetWebhookAsync();
        await _detector.DetectOnceAsync(CancellationToken.None); // Seeds the cursor.

        // Eleven kinds of trouble in one Scope inside the window: ten are announced, the rest
        // fold. The Episodes all open — it is the mailbox the Storm guards, not the table.
        // Distinct words, not distinct numbers: every run of digits is blanked before hashing,
        // so "kind 1" and "kind 2" would be one kind of trouble (ADR 0033).
        foreach (var kind in "abcdefghijkl")
        {
            await LogBoomAsync(_harness.ServiceId, $"trouble of kind {kind}{kind}{kind}");
        }

        await _detector.DetectOnceAsync(CancellationToken.None);
        await _runner.DeliverOnceAsync(CancellationToken.None);

        Assert.Equal(12, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.episodes", 12));
        Assert.Equal(2, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.episodes WHERE alert_folded_into_storm", 2));

        var digest = Assert.Single(_mail.Sent, m => m.Subject.StartsWith("[Bugler] Storm in"));
        Assert.Contains("opened 12 kinds of trouble", digest.TextBody);
        // Ten opening Alerts plus the one digest — the two folded ones sent nothing of their own.
        Assert.Equal(11, _mail.Sent.Count);
        Assert.Equal(11, _chat.Sent.Count);
    }

    private async Task OpenEpisodeAsync()
    {
        await _detector.DetectOnceAsync(CancellationToken.None);
        await LogBoomAsync(_harness.ServiceId);
        await _detector.DetectOnceAsync(CancellationToken.None);
    }

    private Task LogBoomAsync(Guid serviceId, string body = "boom") =>
        _harness.ExecuteSqlAsync($$"""
            INSERT INTO telemetry.log_records
                (service_id, timestamp, severity_number, body, resource_attributes, attributes)
            VALUES ('{{serviceId}}', now(), 17, '{{body}}', '{}', '{}')
            """);
}
