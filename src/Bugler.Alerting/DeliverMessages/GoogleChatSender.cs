using System.Net.Http.Json;

namespace Bugler.Alerting.DeliverMessages;

/// <summary>Posts the simple-text payload a Google Chat incoming webhook takes.</summary>
internal sealed class GoogleChatSender(HttpClient httpClient) : IChatSender
{
    public async Task SendAsync(string webhookUrl, string text, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync(webhookUrl, new { text }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
