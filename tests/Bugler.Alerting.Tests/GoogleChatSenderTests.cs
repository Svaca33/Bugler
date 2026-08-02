using System.Text.Json;
using System.Text.Json.Nodes;
using Bugler.Alerting.DeliverMessages;

namespace Bugler.Alerting.Tests;

public class GoogleChatSenderTests
{
    private static ComposedAlert Alert(string? episodeUrl = "https://bugler.example.com/episodes?episode=1") => new(
        Subject: "[Bugler] Trouble in Eshop acme/prod/web",
        Place: "Eshop acme/prod/web",
        SeverityLabel: "ERROR",
        MatchInstant: "2026-07-29 09:59:58 UTC",
        MatchDetail: "Payment gateway timed out",
        EpisodeUrl: episodeUrl,
        TextBody: "irrelevant here",
        HtmlBody: "irrelevant here");

    private static JsonNode Payload(ComposedAlert alert) =>
        JsonNode.Parse(JsonSerializer.Serialize(GoogleChatSender.BuildPayload(alert)))!;

    [Fact]
    public void The_card_carries_the_header_the_first_log_and_the_button()
    {
        var card = Payload(Alert())["cardsV2"]![0]!["card"]!;

        Assert.Equal("Trouble in Eshop acme/prod/web", (string?)card["header"]!["title"]);
        Assert.Equal("ERROR · 2026-07-29 09:59:58 UTC", (string?)card["header"]!["subtitle"]);

        var widgets = card["sections"]![0]!["widgets"]!.AsArray();
        Assert.Equal(3, widgets.Count);
        Assert.Contains("started logging trouble", (string?)widgets[0]!["textParagraph"]!["text"]);
        Assert.Equal(
            "First log (ERROR, 2026-07-29 09:59:58 UTC)",
            (string?)widgets[1]!["decoratedText"]!["topLabel"]);
        Assert.Equal("Payment gateway timed out", (string?)widgets[1]!["decoratedText"]!["text"]);
        Assert.Equal(
            "https://bugler.example.com/episodes?episode=1",
            (string?)widgets[2]!["buttonList"]!["buttons"]![0]!["onClick"]!["openLink"]!["url"]);
    }

    [Fact]
    public void Card_text_is_chats_html_so_the_log_body_arrives_escaped()
    {
        var alert = Alert() with { MatchDetail = "expected <ul> & got <li>" };

        var widgets = Payload(alert)["cardsV2"]![0]!["card"]!["sections"]![0]!["widgets"]!;
        Assert.Equal(
            "expected &lt;ul&gt; &amp; got &lt;li&gt;",
            (string?)widgets[1]!["decoratedText"]!["text"]);
    }

    [Fact]
    public void Without_an_episode_url_the_card_has_no_button()
    {
        var widgets = Payload(Alert(episodeUrl: null))
            ["cardsV2"]![0]!["card"]!["sections"]![0]!["widgets"]!.AsArray();

        Assert.Equal(2, widgets.Count);
        Assert.All(widgets, w => Assert.Null(w!["buttonList"]));
    }
}
