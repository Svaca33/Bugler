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
        var widgets = new List<object>
        {
            new
            {
                textParagraph = new
                {
                    text = $"<b>{WebUtility.HtmlEncode(alert.Place)}</b> started logging trouble.",
                },
            },
            new
            {
                decoratedText = new
                {
                    topLabel = $"First log ({alert.SeverityLabel}, {alert.FirstLogInstant})",
                    text = WebUtility.HtmlEncode(alert.FirstLogBody),
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
                        new { text = "Open episode", onClick = new { openLink = new { url = alert.EpisodeUrl } } },
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
                            title = $"Trouble in {alert.Place}",
                            subtitle = $"{alert.SeverityLabel} · {alert.FirstLogInstant}",
                        },
                        sections = new[] { new { widgets } },
                    },
                },
            },
        };
    }
}
