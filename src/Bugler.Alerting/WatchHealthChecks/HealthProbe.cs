namespace Bugler.Alerting.WatchHealthChecks;

/// <summary>
/// What one probe saw. <see cref="Detail"/> is the line an Episode keeps as its opening evidence
/// and a message quotes, so it names the address too — the Alert then explains itself without
/// reading settings that may have changed since.
/// </summary>
public sealed record ProbeOutcome(bool Alive, string Detail);

/// <summary>
/// Asks one Service whether it is alive (see CONTEXT.md: Health Check). Two-hundreds are alive
/// and everything else is not — no body is read, so a Service in any language can answer, and a
/// misconfigured URL can leak nothing back but one bit.
/// </summary>
internal sealed class HealthProbe(HttpClient httpClient)
{
    /// <summary>A health endpoint slower than this is, for the purpose of "is it there?", indistinguishable from a dead one.</summary>
    public const int TimeoutSeconds = 5;

    public async Task<ProbeOutcome> ProbeAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            // Headers are enough — the verdict is the status line and nothing below it.
            using var response = await httpClient.GetAsync(
                url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            // Redirects are not followed (see the handler): a health endpoint that bounces the
            // caller to a login page would otherwise be judged by the login page's 200.
            return new ProbeOutcome(
                response.IsSuccessStatusCode, $"HTTP {(int)response.StatusCode} from {url}");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ProbeOutcome(false, $"No answer within {TimeoutSeconds} s from {url}");
        }
        catch (HttpRequestException exception)
        {
            return new ProbeOutcome(false, Describe(exception, url));
        }
    }

    private static string Describe(HttpRequestException exception, string url) =>
        exception.HttpRequestError switch
        {
            HttpRequestError.NameResolutionError => $"Unknown host in {url}",
            HttpRequestError.ConnectionError => $"Could not connect to {url}",
            HttpRequestError.SecureConnectionError => $"TLS handshake failed with {url}",
            _ => $"Request to {url} failed",
        };
}
