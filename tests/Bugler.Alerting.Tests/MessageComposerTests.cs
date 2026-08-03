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
        var alert = MessageComposer.ComposeAlert(
            episode, Identity, "https://bugler.example.com", Language.English);

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
        var alert = MessageComposer.ComposeAlert(
            episode, Identity, "https://bugler.example.com", Language.English);

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

        var alert = MessageComposer.ComposeAlert(
            episode, Identity, "https://bugler.example.com", Language.English);

        Assert.DoesNotContain("<script>", alert.HtmlBody);
        Assert.Contains("&lt;script&gt;", alert.HtmlBody);
        Assert.Contains("&amp; friends", alert.HtmlBody);
        Assert.Contains("<script>alert('x')</script> & friends", alert.TextBody);
    }

    [Fact]
    public void An_unanswered_health_check_speaks_of_silence_rather_than_of_logs()
    {
        var episode = new Episode
        {
            Id = Guid.NewGuid(),
            ServiceId = Identity.Id,
            ApplicationId = Identity.ApplicationId,
            Watch = Watch.HealthCheck,
            Fingerprint = "(health check failing)",
            OpenedAt = new DateTimeOffset(2026, 7, 29, 10, 0, 0, TimeSpan.Zero),
            FirstMatchAt = new DateTimeOffset(2026, 7, 29, 9, 59, 58, TimeSpan.Zero),
            FirstMatchDetail = "HTTP 503 from http://backend:8080/health",
            LastMatchAt = new DateTimeOffset(2026, 7, 29, 11, 0, 0, TimeSpan.Zero),
        };

        var alert = MessageComposer.ComposeAlert(
            episode, Identity, "https://bugler.example.com", Language.English);

        Assert.Equal("[Bugler] No answer from Eshop acme/prod/web", alert.Subject);
        Assert.Contains("stopped answering its health check.", alert.TextBody);
        Assert.Contains("HTTP 503 from http://backend:8080/health", alert.TextBody);
        // No Severity Band exists to name, so the stamp is the moment alone.
        Assert.Null(alert.SeverityLabel);
        Assert.Contains("Health check (2026-07-29 09:59:58 UTC):", alert.TextBody);
        Assert.DoesNotContain("ERROR", alert.TextBody);
        Assert.DoesNotContain("started logging trouble", alert.TextBody);
    }

    [Fact]
    public void Without_a_public_base_url_the_message_carries_no_links()
    {
        var alert = MessageComposer.ComposeAlert(Episode(), Identity, "", Language.English);

        Assert.Null(alert.EpisodeUrl);
        Assert.DoesNotContain("http", alert.TextBody);
        Assert.DoesNotContain("href", alert.HtmlBody);
        Assert.Contains("Payment gateway timed out", alert.TextBody);
    }

    [Fact]
    public void A_czech_recipient_reads_the_alert_in_czech_with_machine_stamps_untouched()
    {
        var episode = Episode();
        var alert = MessageComposer.ComposeAlert(
            episode, Identity, "https://bugler.example.com", Language.Czech);

        Assert.Equal("[Bugler] Potíže v Eshop acme/prod/web", alert.Subject);
        Assert.Contains("začal logovat potíže.", alert.TextBody);
        Assert.Contains($"Epizoda: {alert.EpisodeUrl}", alert.TextBody);
        Assert.Equal("Otevřít epizodu", alert.OpenEpisodeLabel);
        // The button label passes through HtmlEncode, which turns accents into entities.
        Assert.Contains(System.Net.WebUtility.HtmlEncode("Otevřít epizodu"), alert.HtmlBody);
        Assert.Contains("lang=\"cs\"", alert.HtmlBody);
        // The Severity Band and the UTC stamp are the domain's vocabulary, not prose.
        Assert.Contains("První log (ERROR, 2026-07-29 09:59:58 UTC):", alert.TextBody);
    }
}
