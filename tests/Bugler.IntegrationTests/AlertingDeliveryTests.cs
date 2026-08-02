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
        _harness = await BuglerHarness.StartAsync(builder =>
        {
            builder.UseSetting("Mail:Smtp:Host", "smtp.test");
            builder.UseSetting("Mail:Smtp:From", "bugler@test.local");
            builder.UseSetting("Server:PublicBaseUrl", "https://bugler.test");
            builder.ConfigureTestServices(services =>
            {
                services.AddSingleton<IMailSender>(_mail);
                services.AddSingleton<IChatSender>(_chat);
            });
        });
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
    private async Task OpenEpisodeAsync()
    {
        await _detector.DetectOnceAsync(CancellationToken.None);
        await _harness.ExecuteSqlAsync($$"""
            INSERT INTO telemetry.log_records
                (service_id, timestamp, severity_number, body, resource_attributes, attributes)
            VALUES ('{{_harness.ServiceId}}', now(), 17, 'boom', '{}', '{}')
            """);
        await _detector.DetectOnceAsync(CancellationToken.None);
    }
}
