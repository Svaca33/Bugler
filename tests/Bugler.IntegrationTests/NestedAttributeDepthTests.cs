using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bugler.Exploration.GetTraceWaterfall;
using Bugler.Exploration.SearchLogs;
using Google.Protobuf;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;

namespace Bugler.IntegrationTests;

/// <summary>
/// How deeply an attribute may nest is settled by three limits, none of them Bugler's own: the OTLP
/// parser refuses a message nested past 100 levels, <c>JsonDocument.Parse</c> reads no deeper than
/// 64, and the writer that serializes a response stops at 64 counted from the top of that response —
/// so the wrappers around a span's Events are spent against it too. They hold in that order, the
/// parser turning an Export Request away long before either reader could, which is why no sender can
/// store an attribute Exploration then chokes on, and why raising the reader's depth would guard
/// nothing.
///
/// That order is an accident of three defaults, though, and these tests are what makes it a fact.
/// They ask the parser how deep it will go rather than carrying the answer written down: a
/// <c>KvlistValue</c> costs three nested messages per level of JSON and an <c>ArrayValue</c> two, so
/// arrays reach deepest, and each test nests them upward until the parser refuses and then sends the
/// deepest Export Request it accepted. Raise the recursion limit, or give the mapper another wrapper
/// to write, and the probe follows: it fails at the level where the read path gives way, instead of
/// passing on a number that quietly stopped being the maximum.
///
/// Read back through the API rather than through SQL on purpose. jsonb holds any of this without
/// complaint; the two limits that could break are both on the way out, and the tighter of them is
/// the response serializer, which only a real request exercises.
/// </summary>
public sealed class NestedAttributeDepthTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;
    private static readonly byte[] TraceIdBytes = [.. Enumerable.Range(1, 16).Select(i => (byte)i)];
    private const string TraceIdHex = "0102030405060708090a0b0c0d0e0f10";

    /// <summary>Where a probe stops being one: far past any limit a parser plausibly holds.</summary>
    private const int Absurd = 512;

    private const string DeepKey = "deep";
    private const string Leaf = "the bottom";

    private BuglerHarness _harness = null!;

    public async Task InitializeAsync() => _harness = await BuglerHarness.StartAsync();

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task The_deepest_log_attribute_the_parser_accepts_is_read_back_by_the_log_list()
    {
        var depth = DeepestAccepted(d => LogRequest(d).ToByteArray(), ExportLogsServiceRequest.Parser);

        await PostAsync("/v1/logs", LogRequest(depth).ToByteArray());
        await _harness.WaitForRowsAsync("SELECT body FROM telemetry.log_records", expectedCount: 1);

        var response = await _harness.Client.GetAsync("/api/logs");
        Assert.True(
            response.IsSuccessStatusCode,
            $"the log list answered {(int)response.StatusCode} for attributes nested {depth} levels " +
            "deep — as deep as the OTLP parser lets a sender in");

        var item = Assert.Single((await response.Content.ReadFromJsonAsync<SearchLogsResponse>(Json))!.Items);
        Assert.Equal(Leaf, Bottom(item.Attributes.GetProperty(DeepKey), depth).GetString());
        Assert.Equal(Leaf, Bottom(item.ResourceAttributes.GetProperty(DeepKey), depth).GetString());
    }

    /// <summary>
    /// The deepest JSON Bugler can be made to write is not a log record's attributes but a span
    /// Event's: the array and object the mapper wraps each Event in cost the sender nothing and add
    /// two levels, and the waterfall's own response adds three more around them.
    /// </summary>
    [Fact]
    public async Task The_deepest_span_attribute_the_parser_accepts_is_read_back_by_the_waterfall()
    {
        var depth = DeepestAccepted(d => TraceRequest(d).ToByteArray(), ExportTraceServiceRequest.Parser);

        await PostAsync("/v1/traces", TraceRequest(depth).ToByteArray());
        await _harness.WaitForRowsAsync("SELECT name FROM telemetry.spans", expectedCount: 1);

        var response = await _harness.Client.GetAsync($"/api/traces/{TraceIdHex}");
        Assert.True(
            response.IsSuccessStatusCode,
            $"the waterfall answered {(int)response.StatusCode} for attributes nested {depth} levels " +
            "deep — as deep as the OTLP parser lets a sender in");

        var span = Assert.Single((await response.Content.ReadFromJsonAsync<TraceDetailResponse>(Json))!.Spans);
        Assert.Equal(Leaf, Bottom(span.Attributes.GetProperty(DeepKey), depth).GetString());
        Assert.Equal(Leaf, Bottom(Attributes(span.Events), depth).GetString());
        Assert.Equal(Leaf, Bottom(Attributes(span.Links), depth).GetString());
    }

    /// <summary>
    /// The deepest Export Request this parser accepts, asked of the parser rather than remembered.
    /// Every level is built and parsed whole, so the answer counts the Export Request's own framing
    /// exactly as the receiver's parse does.
    /// </summary>
    private static int DeepestAccepted(Func<int, byte[]> serialize, MessageParser parser)
    {
        for (var depth = 1; depth <= Absurd; depth++)
        {
            try
            {
                parser.ParseFrom(serialize(depth));
            }
            catch (InvalidProtocolBufferException)
            {
                Assert.True(depth > 1, "the parser refused the shallowest attribute there is");
                return depth - 1;
            }
        }

        throw new InvalidOperationException(
            $"the parser accepted {Absurd} levels of nesting, so this probe no longer finds its ceiling");
    }

    /// <summary>Walks an attribute down to the value the sender put at the bottom of it.</summary>
    private static JsonElement Bottom(JsonElement value, int depth)
    {
        for (var level = 0; level < depth; level++)
        {
            value = value.EnumerateArray().Single();
        }

        return value;
    }

    /// <summary>The deep attribute of the single Event or Link the span carries.</summary>
    private static JsonElement Attributes(JsonElement eventsOrLinks) =>
        eventsOrLinks.EnumerateArray().Single().GetProperty("attributes").GetProperty(DeepKey);

    private static AnyValue Nested(int depth)
    {
        var value = new AnyValue { StringValue = Leaf };
        for (var level = 0; level < depth; level++)
        {
            value = new AnyValue { ArrayValue = new ArrayValue { Values = { value } } };
        }

        return value;
    }

    private static KeyValue DeepAttribute(int depth) => new() { Key = DeepKey, Value = Nested(depth) };

    private static ExportLogsServiceRequest LogRequest(int depth)
    {
        var record = new LogRecord
        {
            TimeUnixNano = ToNano(DateTime.UtcNow),
            SeverityNumber = SeverityNumber.Info,
            Body = new AnyValue { StringValue = "As deep as it goes" },
            Attributes = { DeepAttribute(depth) },
        };
        var resourceLogs = new ResourceLogs
        {
            Resource = new Resource { Attributes = { DeepAttribute(depth) } },
            ScopeLogs = { new ScopeLogs { LogRecords = { record } } },
        };

        return new ExportLogsServiceRequest { ResourceLogs = { resourceLogs } };
    }

    private static ExportTraceServiceRequest TraceRequest(int depth)
    {
        var now = DateTime.UtcNow;
        var span = new Span
        {
            TraceId = ByteString.CopyFrom(TraceIdBytes),
            SpanId = ByteString.CopyFrom([.. Enumerable.Repeat((byte)1, 8)]),
            Name = "GET /deep",
            Kind = Span.Types.SpanKind.Server,
            StartTimeUnixNano = ToNano(now.AddMilliseconds(-50)),
            EndTimeUnixNano = ToNano(now),
            Attributes = { DeepAttribute(depth) },
            Events =
            {
                new Span.Types.Event
                {
                    Name = "deep",
                    TimeUnixNano = ToNano(now.AddMilliseconds(-25)),
                    Attributes = { DeepAttribute(depth) },
                },
            },
            Links =
            {
                new Span.Types.Link
                {
                    TraceId = ByteString.CopyFrom(TraceIdBytes),
                    SpanId = ByteString.CopyFrom([.. Enumerable.Repeat((byte)2, 8)]),
                    Attributes = { DeepAttribute(depth) },
                },
            },
        };
        var resourceSpans = new ResourceSpans
        {
            Resource = new Resource { Attributes = { DeepAttribute(depth) } },
            ScopeSpans = { new ScopeSpans { Spans = { span } } },
        };

        return new ExportTraceServiceRequest { ResourceSpans = { resourceSpans } };
    }

    private static ulong ToNano(DateTime utc) => (ulong)(utc - DateTime.UnixEpoch).Ticks * 100;

    private async Task PostAsync(string url, byte[] payload)
    {
        var content = new ByteArrayContent(payload);
        content.Headers.ContentType = new("application/x-protobuf");
        var message = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _harness.ApiKey);
        var response = await _harness.Client.SendAsync(message);
        response.EnsureSuccessStatusCode();
    }
}
