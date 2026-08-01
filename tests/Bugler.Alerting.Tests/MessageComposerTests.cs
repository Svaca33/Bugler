using Bugler.Alerting.DeliverMessages;
using Bugler.Alerting.Episodes;
using Bugler.Registry.Contracts;
using Bugler.SharedKernel;

namespace Bugler.Alerting.Tests;

public class MessageComposerTests
{
    private static readonly CatalogService Identity = new(
        ServiceId.New(), ApplicationId.New(), "Eshop", "acme", "prod", "web");

    private static Episode Episode() => new()
    {
        Id = Guid.NewGuid(),
        ServiceId = Identity.Id,
        ApplicationId = Identity.ApplicationId,
        Fingerprint = "Payment gateway timed out",
        OpenedAt = new DateTimeOffset(2026, 7, 29, 10, 0, 0, TimeSpan.Zero),
        FirstLogId = 42,
        FirstLogTimestamp = new DateTimeOffset(2026, 7, 29, 9, 59, 58, TimeSpan.Zero),
        FirstLogSeverity = 17,
        FirstLogBody = "Payment gateway timed out",
        ErrorCount = 128,
        WarnCount = 1,
        LastMatchAt = new DateTimeOffset(2026, 7, 29, 11, 0, 0, TimeSpan.Zero),
    };

    [Fact]
    public void The_alert_names_the_service_and_shows_the_first_log()
    {
        var episode = Episode();
        var message = MessageComposer.ComposeAlert(episode, Identity, "https://bugler.example.com");

        Assert.Equal("[Bugler] Trouble in Eshop acme/prod/web", message.Subject);
        Assert.Contains("First log (ERROR, 2026-07-29 09:59:58 UTC):", message.Text);
        Assert.Contains("Payment gateway timed out", message.Text);
        Assert.Contains(
            $"Episode: https://bugler.example.com/episodes?episode={episode.Id}", message.Text);
    }

    [Fact]
    public void Without_a_public_base_url_the_message_carries_no_links()
    {
        var message = MessageComposer.ComposeAlert(Episode(), Identity, "");

        Assert.DoesNotContain("http", message.Text);
        Assert.Contains("Payment gateway timed out", message.Text);
    }
}
