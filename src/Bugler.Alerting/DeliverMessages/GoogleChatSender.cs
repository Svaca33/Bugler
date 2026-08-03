using System.Net;
using System.Net.Http.Json;

namespace Bugler.Alerting.DeliverMessages;

/// <summary>
/// Renders the Alert as the cardsV2 payload a Google Chat incoming webhook takes: header,
/// first log, and an Open episode button. Card text is Chat's limited HTML, so the log body
/// and service names are escaped on the way in.
/// </summary>
internal sealed class GoogleChatSender(HttpClient httpClient) : IChatSender
{
    public async Task SendAsync(string webhookUrl, ComposedAlert alert, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync(webhookUrl, BuildPayload(alert), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    internal static object BuildPayload(ComposedAlert alert)
    {
        // The band joins the stamp only where the Alert's Watch deals in Severity Bands.
        var stamp = alert.SeverityLabel is null
            ? alert.MatchInstant
            : $"{alert.SeverityLabel}, {alert.MatchInstant}";
        var subtitle = alert.SeverityLabel is null
            ? alert.MatchInstant
            : $"{alert.SeverityLabel} · {alert.MatchInstant}";

        var widgets = new List<object>
        {
            new
            {
                textParagraph = new
                {
                    text = $"<b>{WebUtility.HtmlEncode(alert.Place)}</b> {WebUtility.HtmlEncode(alert.Opening)}",
                },
            },
            new
            {
                decoratedText = new
                {
                    topLabel = $"{alert.EvidenceLabel} ({stamp})",
                    text = WebUtility.HtmlEncode(alert.MatchDetail),
                    wrapText = true,
                },
            },
        };

        if (alert.EpisodeUrl is not null)
        {
            widgets.Add(new
            {
                buttonList = new
                {
                    buttons = new[]
                    {
                        new { text = alert.OpenEpisodeLabel, onClick = new { openLink = new { url = alert.EpisodeUrl } } },
                    },
                },
            });
        }

        return new
        {
            cardsV2 = new[]
            {
                new
                {
                    cardId = "bugler-alert",
                    card = new
                    {
                        header = new
                        {
                            title = alert.Headline,
                            subtitle,
                        },
                        sections = new[] { new { widgets } },
                    },
                },
            },
        };
    }
}
