using System.Net;
using System.Net.Http.Headers;
using Google.Protobuf;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;

namespace Bugler.IntegrationTests;

/// <summary>
/// Whole trace write path against a real PostgreSQL: authenticate, ingest via OTLP/HTTP,
/// and observe the spans the background writer persisted.
/// </summary>
public sealed class OtlpTraceIngestTests : IAsyncLifetime
{
    private BuglerHarness _harness = null!;

    public async Task InitializeAsync() => _harness = await BuglerHarness.StartAsync();

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task Export_with_valid_key_persists_spans()
    {
        var now = DateTime.UtcNow;
        var traceId = ByteString.CopyFrom(Enumerable.Range(1, 16).Select(i => (byte)i).ToArray());
        var parent = Span("root", traceId, spanIdSeed: 1, now, durationMs: 50);
        var child = Span("db-query", traceId, spanIdSeed: 2, now.AddMilliseconds(10), durationMs: 20);

        var response = await PostTracesAsync(_harness.ApiKey, parent, child);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var names = await _harness.WaitForRowsAsync(
            "SELECT name FROM telemetry.spans ORDER BY id", expectedCount: 2);
        Assert.Contains("root", names);
        Assert.Contains("db-query", names);
    }

    private static Span Span(string name, ByteString traceId, byte spanIdSeed, DateTime start, int durationMs)
    {
        return new Span
        {
            TraceId = traceId,
            SpanId = ByteString.CopyFrom(Enumerable.Repeat(spanIdSeed, 8).Select(i => (byte)i).ToArray()),
            Name = name,
            Kind = OpenTelemetry.Proto.Trace.V1.Span.Types.SpanKind.Internal,
            StartTimeUnixNano = (ulong)(start - DateTime.UnixEpoch).Ticks * 100,
            EndTimeUnixNano = (ulong)(start.AddMilliseconds(durationMs) - DateTime.UnixEpoch).Ticks * 100,
        };
    }

    private async Task<HttpResponseMessage> PostTracesAsync(string apiKey, params Span[] spans)
    {
        var request = new ExportTraceServiceRequest();
        var resourceSpans = new ResourceSpans
        {
            Resource = new Resource
            {
                Attributes =
                {
                    new KeyValue { Key = "service.name", Value = new AnyValue { StringValue = "web" } },
                },
            },
        };
        var scopeSpans = new ScopeSpans();
        scopeSpans.Spans.AddRange(spans);
        resourceSpans.ScopeSpans.Add(scopeSpans);
        request.ResourceSpans.Add(resourceSpans);

        var content = new ByteArrayContent(request.ToByteArray());
        content.Headers.ContentType = new("application/x-protobuf");
        var message = new HttpRequestMessage(HttpMethod.Post, "/v1/traces") { Content = content };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        return await _harness.Client.SendAsync(message);
    }
}
