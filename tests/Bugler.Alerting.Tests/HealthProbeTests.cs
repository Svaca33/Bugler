using System.Net;
using Bugler.Alerting.WatchHealthChecks;

namespace Bugler.Alerting.Tests;

/// <summary>
/// The whole verdict: two-hundreds are alive and nothing else is. The body is never read, so a
/// Service in any language can answer and a misconfigured address can leak nothing back.
/// </summary>
public class HealthProbeTests
{
    private const string Url = "http://backend:8080/health";

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.NoContent)]
    public async Task A_two_hundred_is_alive(HttpStatusCode status)
    {
        var outcome = await Probe(new HttpResponseMessage(status));

        Assert.True(outcome.Alive);
        Assert.Equal($"HTTP {(int)status} from {Url}", outcome.Detail);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task Anything_else_is_not(HttpStatusCode status)
    {
        var outcome = await Probe(new HttpResponseMessage(status));

        Assert.False(outcome.Alive);
        Assert.Equal($"HTTP {(int)status} from {Url}", outcome.Detail);
    }

    [Fact]
    public async Task A_redirect_is_a_failure_rather_than_something_to_follow()
    {
        // A health endpoint that bounces to a login page is broken; following it would have the
        // probe judge the login page instead, which answers 200 for ever.
        var outcome = await Probe(new HttpResponseMessage(HttpStatusCode.Found));

        Assert.False(outcome.Alive);
        Assert.Equal($"HTTP 302 from {Url}", outcome.Detail);
    }

    [Fact]
    public async Task Silence_past_the_timeout_reads_as_death()
    {
        var outcome = await Probe(new TaskCanceledException());

        Assert.False(outcome.Alive);
        Assert.Equal($"No answer within {HealthProbe.TimeoutSeconds} s from {Url}", outcome.Detail);
    }

    [Theory]
    [InlineData(HttpRequestError.ConnectionError, "Could not connect to")]
    [InlineData(HttpRequestError.NameResolutionError, "Unknown host in")]
    [InlineData(HttpRequestError.SecureConnectionError, "TLS handshake failed with")]
    public async Task A_transport_failure_says_which_one_it_was(
        HttpRequestError error, string expected)
    {
        var outcome = await Probe(new HttpRequestException(error));

        Assert.False(outcome.Alive);
        Assert.Equal($"{expected} {Url}", outcome.Detail);
    }

    private static Task<ProbeOutcome> Probe(HttpResponseMessage response) =>
        new HealthProbe(new HttpClient(new StubHandler(response))).ProbeAsync(Url, default);

    private static Task<ProbeOutcome> Probe(Exception failure) =>
        new HealthProbe(new HttpClient(new StubHandler(failure))).ProbeAsync(Url, default);

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage? _response;
        private readonly Exception? _failure;

        public StubHandler(HttpResponseMessage response) => _response = response;

        public StubHandler(Exception failure) => _failure = failure;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            _failure is not null
                ? Task.FromException<HttpResponseMessage>(_failure)
                : Task.FromResult(_response!);
    }
}
