using Bugler.Ai;
using Bugler.Alerting.Episodes;
using Bugler.Alerting.WriteReadings;
using Bugler.Registry.Contracts;
using Bugler.SharedKernel;

namespace Bugler.Alerting.Tests;

public class ReadingPromptTests
{
    private static Episode Episode(Watch watch = Watch.Logs) => new()
    {
        Id = Guid.CreateVersion7(),
        ServiceId = ServiceId.New(),
        ApplicationId = ApplicationId.New(),
        Watch = watch,
        Fingerprint = "Payment gateway timed out",
        OpenedAt = new DateTimeOffset(2026, 8, 9, 12, 10, 0, TimeSpan.Zero),
        FirstMatchLogId = watch == Watch.Logs ? 42 : null,
        FirstMatchAt = new DateTimeOffset(2026, 8, 9, 12, 9, 58, TimeSpan.Zero),
        FirstMatchSeverity = watch == Watch.Logs ? (short)17 : null,
        FirstMatchDetail = "Payment gateway timed out after 30s",
        ErrorCount = 3,
        WarnCount = 1,
        LastMatchAt = new DateTimeOffset(2026, 8, 9, 12, 10, 0, TimeSpan.Zero),
    };

    private static readonly CatalogService Identity = new(
        ServiceId.New(), ApplicationId.New(), "Eshop", "acme", "prod", "web");

    [Fact]
    public void The_prompt_carries_the_evidence_and_names_the_release_gap()
    {
        var evidence = new ReadingEvidence(
            OpeningAttributes: """{"exception.type":"TimeoutException"}""",
            PriorLogs:
            [
                new EvidenceLog(new DateTime(2026, 8, 9, 12, 9, 30, DateTimeKind.Utc), 9, "Order 123 placed"),
            ],
            LastRelease: new ReleaseFact(
                "2.3.1", "2.3.0", new DateTime(2026, 8, 9, 12, 6, 0, DateTimeKind.Utc)));

        var prompt = ReadingPrompt.Compose(Episode(), Identity, evidence);

        Assert.Contains("Eshop acme/prod/web", prompt.Input);
        Assert.Contains("Payment gateway timed out after 30s", prompt.Input);
        Assert.Contains("TimeoutException", prompt.Input);
        Assert.Contains("Order 123 placed", prompt.Input);
        Assert.Contains("2.3.1 (from 2.3.0)", prompt.Input);
        Assert.Contains("4 minutes before the trouble opened", prompt.Input);
        Assert.Contains("\"en\"", prompt.Instructions);
        Assert.Contains("\"cs\"", prompt.Instructions);
    }

    [Fact]
    public void A_health_check_prompt_says_the_service_went_silent()
    {
        var prompt = ReadingPrompt.Compose(
            Episode(Watch.HealthCheck),
            Identity,
            new ReadingEvidence(null, [], null));

        Assert.Contains("stopped answering", prompt.Input);
        Assert.Contains("logged nothing before the opening", prompt.Input);
        Assert.Contains("No release of this service is on record", prompt.Input);
    }

    [Fact]
    public void The_input_never_exceeds_its_ceiling()
    {
        var flood = Enumerable.Range(0, 500)
            .Select(i => new EvidenceLog(
                new DateTime(2026, 8, 9, 12, 0, 0, DateTimeKind.Utc), 9, new string('x', 400)))
            .ToList();

        var prompt = ReadingPrompt.Compose(
            Episode(), Identity, new ReadingEvidence(null, flood, null));

        Assert.True(prompt.Input.Length <= 16_000 + 500); // The cap plus at most one log line.
    }

    [Fact]
    public void The_answer_is_read_even_when_the_model_wraps_it_in_fences()
    {
        var (english, czech) = ReadingPrompt.ParseAnswer(
            """
            ```json
            {"en": "The gateway began timing out.", "cs": "Brána začala vypršívat."}
            ```
            """);

        Assert.Equal("The gateway began timing out.", english);
        Assert.Equal("Brána začala vypršívat.", czech);
    }

    [Theory]
    [InlineData("no json at all")]
    [InlineData("""{"en": "only english"}""")]
    [InlineData("""{"en": "", "cs": "jen čeština"}""")]
    public void An_answer_missing_either_language_is_refused(string answer) =>
        Assert.Throws<AiException>(() => ReadingPrompt.ParseAnswer(answer));
}
