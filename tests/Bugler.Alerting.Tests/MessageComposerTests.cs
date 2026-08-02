using Bugler.Alerting.DeliverMessages;
using Bugler.Alerting.Episodes;
using Bugler.Registry.Contracts;
using Bugler.SharedKernel;

namespace Bugler.Alerting.Tests;

public class MessageComposerTests
{
    private static readonly CatalogService Identity = new(
        ServiceId.New(), ApplicationId.New(), "Eshop", "acme", "prod", "web");

    private static Episode Episode(string firstLogBody = "Payment gateway timed out") => new()
    {
        Id = Guid.NewGuid(),
        ServiceId = Identity.Id,
        ApplicationId = Identity.ApplicationId,
        Watch = Watch.Logs,
        Fingerprint = "Payment gateway timed out",
        OpenedAt = new DateTimeOffset(2026, 7, 29, 10, 0, 0, TimeSpan.Zero),
        FirstMatchLogId = 42,
        FirstMatchAt = new DateTimeOffset(2026, 7, 29, 9, 59, 58, TimeSpan.Zero),
        FirstMatchSeverity = 17,
        FirstMatchDetail = firstLogBody,
        ErrorCount = 128,
        WarnCount = 1,
        LastMatchAt = new DateTimeOffset(2026, 7, 29, 11, 0, 0, TimeSpan.Zero),
    };

    [Fact]
    public void The_alert_names_the_service_and_shows_the_first_log()
    {
        var episode = Episode();
        var alert = MessageComposer.ComposeAlert(episode, Identity, "https://bugler.example.com");

        Assert.Equal("[Bugler] Trouble in Eshop acme/prod/web", alert.Subject);
        Assert.Equal("Eshop acme/prod/web", alert.Place);
        Assert.Equal("ERROR", alert.SeverityLabel);
        Assert.Contains("First log (ERROR, 2026-07-29 09:59:58 UTC):", alert.TextBody);
        Assert.Contains("Payment gateway timed out", alert.TextBody);
        Assert.Equal(
            $"https://bugler.example.com/episodes?episode={episode.Id}", alert.EpisodeUrl);
        Assert.Contains($"Episode: {alert.EpisodeUrl}", alert.TextBody);
    }

    [Fact]
    public void The_html_body_carries_the_same_facts_and_the_episode_link()
    {
        var episode = Episode();
        var alert = MessageComposer.ComposeAlert(episode, Identity, "https://bugler.example.com");

        Assert.Contains("Eshop acme/prod/web", alert.HtmlBody);
        Assert.Contains("First log (ERROR, 2026-07-29 09:59:58 UTC):", alert.HtmlBody);
        Assert.Contains("Payment gateway timed out", alert.HtmlBody);
        Assert.Contains($"href=\"{alert.EpisodeUrl}\"", alert.HtmlBody);
        Assert.Contains("Open episode", alert.HtmlBody);
    }

    [Fact]
    public void The_html_body_escapes_markup_in_the_log()
    {
        var episode = Episode("Rejected <script>alert('x')</script> & friends");

        var alert = MessageComposer.ComposeAlert(episode, Identity, "https://bugler.example.com");

        Assert.DoesNotContain("<script>", alert.HtmlBody);
        Assert.Contains("&lt;script&gt;", alert.HtmlBody);
        Assert.Contains("&amp; friends", alert.HtmlBody);
        Assert.Contains("<script>alert('x')</script> & friends", alert.TextBody);
    }

    [Fact]
    public void Without_a_public_base_url_the_message_carries_no_links()
    {
        var alert = MessageComposer.ComposeAlert(Episode(), Identity, "");

        Assert.Null(alert.EpisodeUrl);
        Assert.DoesNotContain("http", alert.TextBody);
        Assert.DoesNotContain("href", alert.HtmlBody);
        Assert.Contains("Payment gateway timed out", alert.TextBody);
    }
}
