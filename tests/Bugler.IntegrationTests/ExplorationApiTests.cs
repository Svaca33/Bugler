using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bugler.Exploration.GetTraceWaterfall;
using Bugler.Exploration.ListTraces;
using Bugler.Exploration.ObservedKeys;
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
/// The read path over real data: telemetry ingested via OTLP/HTTP is then
/// searched, filtered, and correlated through the Exploration API.
/// </summary>
public sealed class ExplorationApiTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = JsonSerializerOptions.Web;
    private static readonly byte[] TraceIdBytes = [.. Enumerable.Range(1, 16).Select(i => (byte)i)];
    private const string TraceIdHex = "0102030405060708090a0b0c0d0e0f10";

    private BuglerHarness _harness = null!;

    public async Task InitializeAsync()
    {
        _harness = await BuglerHarness.StartAsync();
        await IngestSampleTelemetryAsync();
    }

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task Logs_can_be_searched_filtered_and_correlated()
    {
        var all = await GetAsync<SearchLogsResponse>("/api/logs");
        Assert.Equal(3, all.Items.Count);
        Assert.Equal(_harness.ServiceId, all.Items[0].ServiceId);

        var warnings = await GetAsync<SearchLogsResponse>("/api/logs?severityMin=13");
        Assert.Equal("Payment failed for acme", Assert.Single(warnings.Items).Body);

        var acmeOnly = await GetAsync<SearchLogsResponse>($"/api/logs?attr={FilterJson("acme", "tenant.id")}");
        Assert.Equal(2, acmeOnly.Items.Count);
        Assert.All(acmeOnly.Items, item =>
            Assert.Equal("acme", item.Attributes.GetProperty("tenant.id").GetString()));

        var search = await GetAsync<SearchLogsResponse>("/api/logs?q=payment%20failed");
        Assert.Equal("Payment failed for acme", Assert.Single(search.Items).Body);

        var correlated = await GetAsync<SearchLogsResponse>($"/api/logs?traceId={TraceIdHex}");
        Assert.Equal(TraceIdHex, Assert.Single(correlated.Items).TraceId);

        var detail = await GetAsync<LogRecordDto>($"/api/logs/{all.Items[0].Id}");
        Assert.Equal(all.Items[0].Body, detail.Body);
    }

    [Fact]
    public async Task Logs_filter_by_attribute_and_resource_paths()
    {
        var byLiteralKey = await GetAsync<SearchLogsResponse>($"/api/logs?attr={FilterJson("acme", "tenant.id")}");
        Assert.Equal(2, byLiteralKey.Items.Count);

        var byNestedNumber = await GetAsync<SearchLogsResponse>($"/api/logs?attr={FilterJson("42", "order", "total")}");
        Assert.Equal("Checkout started", Assert.Single(byNestedNumber.Items).Body);

        var byResource = await GetAsync<SearchLogsResponse>($"/api/logs?res={FilterJson("web", "service.name")}");
        Assert.Equal(3, byResource.Items.Count);

        var combined = await GetAsync<SearchLogsResponse>(
            $"/api/logs?attr={FilterJson("acme", "tenant.id")}&attr={FilterJson("42", "order", "total")}");
        Assert.Equal("Checkout started", Assert.Single(combined.Items).Body);

        var noMatch = await GetAsync<SearchLogsResponse>($"/api/logs?res={FilterJson("other", "service.name")}");
        Assert.Empty(noMatch.Items);

        var invalid = await _harness.Client.GetAsync("/api/logs?attr=notjson");
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task Traces_match_only_when_a_single_span_satisfies_all_filters()
    {
        var sameSpan = await GetAsync<ListTracesResponse>(
            $"/api/traces?attr={FilterJson("GET", "http.method")}&attr={FilterJson("beta", "tenant.id")}");
        Assert.Equal(TraceIdHex, Assert.Single(sameSpan.Items).TraceId);

        var crossSpan = await GetAsync<ListTracesResponse>(
            $"/api/traces?attr={FilterJson("GET", "http.method")}&attr={FilterJson("acme", "tenant.id")}");
        Assert.Empty(crossSpan.Items);

        var byResource = await GetAsync<ListTracesResponse>($"/api/traces?res={FilterJson("web", "service.name")}");
        Assert.Single(byResource.Items);
    }

    [Fact]
    public async Task Observed_keys_list_scalar_leaf_paths_from_the_sample()
    {
        var logKeys = await GetAsync<ObservedKeysResponse>("/api/logs/keys");
        Assert.Contains(logKeys.Items, k => k.Scope == "attribute" && k.Path.SequenceEqual(["tenant.id"]));
        Assert.Contains(logKeys.Items, k => k.Scope == "attribute" && k.Path.SequenceEqual(["order", "total"]));
        Assert.Contains(logKeys.Items, k => k.Scope == "resource" && k.Path.SequenceEqual(["service.name"]));
        Assert.DoesNotContain(logKeys.Items, k => k.Path.SequenceEqual(["flags"]));
        Assert.DoesNotContain(logKeys.Items, k => k.Path.SequenceEqual(["order"]));

        var traceKeys = await GetAsync<ObservedKeysResponse>("/api/traces/keys");
        Assert.Contains(traceKeys.Items, k => k.Scope == "attribute" && k.Path.SequenceEqual(["http.method"]));
        Assert.Contains(traceKeys.Items, k => k.Scope == "resource" && k.Path.SequenceEqual(["service.name"]));
    }

    [Fact]
    public async Task Traces_are_listed_and_expanded_into_a_waterfall()
    {
        var traces = await GetAsync<ListTracesResponse>("/api/traces");
        var summary = Assert.Single(traces.Items);
        Assert.Equal(TraceIdHex, summary.TraceId);
        Assert.Equal(2, summary.SpanCount);
        Assert.Equal("GET /checkout", summary.RootName);
        Assert.True(summary.HasError);
        Assert.True(summary.DurationMs >= 50);

        var errorsOnly = await GetAsync<ListTracesResponse>("/api/traces?errorsOnly=true");
        Assert.Single(errorsOnly.Items);

        var waterfall = await GetAsync<TraceDetailResponse>($"/api/traces/{TraceIdHex}");
        Assert.Equal(2, waterfall.Spans.Count);
        Assert.Equal("GET /checkout", waterfall.Spans[0].Name);
        Assert.Equal("charge-card", waterfall.Spans[1].Name);
        Assert.Equal(waterfall.Spans[0].SpanId, waterfall.Spans[1].ParentSpanId);
        Assert.Equal("web", waterfall.Spans[0].ResourceAttributes.GetProperty("service.name").GetString());
    }

    [Fact]
    public async Task Source_filter_addresses_services_by_facet_not_by_registration()
    {
        // A second sender of the same deployment: own registration, own key, same namespace/environment.
        var (workerId, workerKey) = await _harness.SeedServiceAsync(
            _harness.ApplicationId, "acme", "prod", "worker");
        await IngestOneLogAsync(workerKey, "Queue drained");
        await _harness.WaitForRowsAsync("SELECT body FROM telemetry.log_records", expectedCount: 4);

        var wholeDeployment = await GetAsync<SearchLogsResponse>("/api/logs?namespace=acme&environment=prod");
        Assert.Equal(4, wholeDeployment.Items.Count);
        Assert.Contains(wholeDeployment.Items, item => item.ServiceId == workerId);
        Assert.Contains(wholeDeployment.Items, item => item.ServiceId == _harness.ServiceId);

        var workerOnly = await GetAsync<SearchLogsResponse>(
            "/api/logs?namespace=acme&environment=prod&service=worker");
        Assert.Equal("Queue drained", Assert.Single(workerOnly.Items).Body);

        var everyWeb = await GetAsync<SearchLogsResponse>("/api/logs?service=web");
        Assert.Equal(3, everyWeb.Items.Count);

        var unknownFacet = await GetAsync<SearchLogsResponse>("/api/logs?namespace=vysocina");
        Assert.Empty(unknownFacet.Items);
    }

    private static string FilterJson(string value, params string[] path) =>
        Uri.EscapeDataString(JsonSerializer.Serialize(new { path, value }));

    private async Task IngestOneLogAsync(string apiKey, string body)
    {
        var logs = new ExportLogsServiceRequest();
        var resourceLogs = new ResourceLogs();
        var scopeLogs = new ScopeLogs();
        scopeLogs.LogRecords.Add(new LogRecord
        {
            TimeUnixNano = ToNano(DateTime.UtcNow),
            SeverityNumber = SeverityNumber.Info,
            Body = new AnyValue { StringValue = body },
        });
        resourceLogs.ScopeLogs.Add(scopeLogs);
        logs.ResourceLogs.Add(resourceLogs);
        await PostProtobufAsync("/v1/logs", logs.ToByteArray(), apiKey);
    }

    private async Task<T> GetAsync<T>(string url)
    {
        var response = await _harness.Client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private async Task IngestSampleTelemetryAsync()
    {
        var now = DateTime.UtcNow;

        var logs = new ExportLogsServiceRequest();
        var resourceLogs = new ResourceLogs
        {
            Resource = new Resource
            {
                Attributes = { new KeyValue { Key = "service.name", Value = new AnyValue { StringValue = "web" } } },
            },
        };
        var scopeLogs = new ScopeLogs();
        var checkout = Log(now.AddSeconds(-3), SeverityNumber.Info, "Checkout started", tenant: "acme");
        checkout.Attributes.Add(new KeyValue
        {
            Key = "order",
            Value = new AnyValue
            {
                KvlistValue = new KeyValueList
                {
                    Values = { new KeyValue { Key = "total", Value = new AnyValue { IntValue = 42 } } },
                },
            },
        });
        checkout.Attributes.Add(new KeyValue
        {
            Key = "flags",
            Value = new AnyValue
            {
                ArrayValue = new ArrayValue { Values = { new AnyValue { StringValue = "vip" } } },
            },
        });
        scopeLogs.LogRecords.Add(checkout);
        scopeLogs.LogRecords.Add(Log(now.AddSeconds(-2), SeverityNumber.Warn, "Payment failed for acme", tenant: "acme"));
        scopeLogs.LogRecords.Add(Log(now.AddSeconds(-1), SeverityNumber.Info, "Cart emptied", tenant: "globex", traceId: TraceIdBytes));
        resourceLogs.ScopeLogs.Add(scopeLogs);
        logs.ResourceLogs.Add(resourceLogs);
        await PostProtobufAsync("/v1/logs", logs.ToByteArray());

        var traces = new ExportTraceServiceRequest();
        var resourceSpans = new ResourceSpans
        {
            Resource = new Resource
            {
                Attributes = { new KeyValue { Key = "service.name", Value = new AnyValue { StringValue = "web" } } },
            },
        };
        var scopeSpans = new ScopeSpans();
        var rootSpanId = ByteString.CopyFrom([.. Enumerable.Repeat((byte)1, 8)]);
        scopeSpans.Spans.Add(new Span
        {
            TraceId = ByteString.CopyFrom(TraceIdBytes),
            SpanId = rootSpanId,
            Name = "GET /checkout",
            Kind = Span.Types.SpanKind.Server,
            StartTimeUnixNano = ToNano(now.AddMilliseconds(-100)),
            EndTimeUnixNano = ToNano(now.AddMilliseconds(-30)),
            Status = new Status { Code = Status.Types.StatusCode.Error, Message = "payment declined" },
            Attributes =
            {
                new KeyValue { Key = "http.method", Value = new AnyValue { StringValue = "GET" } },
                new KeyValue { Key = "tenant.id", Value = new AnyValue { StringValue = "beta" } },
            },
        });
        scopeSpans.Spans.Add(new Span
        {
            TraceId = ByteString.CopyFrom(TraceIdBytes),
            SpanId = ByteString.CopyFrom([.. Enumerable.Repeat((byte)2, 8)]),
            ParentSpanId = rootSpanId,
            Name = "charge-card",
            Kind = Span.Types.SpanKind.Client,
            StartTimeUnixNano = ToNano(now.AddMilliseconds(-90)),
            EndTimeUnixNano = ToNano(now.AddMilliseconds(-40)),
            Attributes =
            {
                new KeyValue { Key = "http.method", Value = new AnyValue { StringValue = "POST" } },
                new KeyValue { Key = "tenant.id", Value = new AnyValue { StringValue = "acme" } },
            },
        });
        resourceSpans.ScopeSpans.Add(scopeSpans);
        traces.ResourceSpans.Add(resourceSpans);
        await PostProtobufAsync("/v1/traces", traces.ToByteArray());

        await _harness.WaitForRowsAsync("SELECT body FROM telemetry.log_records", expectedCount: 3);
        await _harness.WaitForRowsAsync("SELECT name FROM telemetry.spans", expectedCount: 2);
    }

    private static LogRecord Log(
        DateTime time, SeverityNumber severity, string body, string tenant, byte[]? traceId = null)
    {
        var record = new LogRecord
        {
            TimeUnixNano = ToNano(time),
            SeverityNumber = severity,
            Body = new AnyValue { StringValue = body },
            Attributes = { new KeyValue { Key = "tenant.id", Value = new AnyValue { StringValue = tenant } } },
        };
        if (traceId is not null)
        {
            record.TraceId = ByteString.CopyFrom(traceId);
            record.SpanId = ByteString.CopyFrom([.. Enumerable.Repeat((byte)1, 8)]);
        }

        return record;
    }

    private static ulong ToNano(DateTime utc) => (ulong)(utc - DateTime.UnixEpoch).Ticks * 100;

    private async Task PostProtobufAsync(string url, byte[] payload, string? apiKey = null)
    {
        var content = new ByteArrayContent(payload);
        content.Headers.ContentType = new("application/x-protobuf");
        var message = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey ?? _harness.ApiKey);
        var response = await _harness.Client.SendAsync(message);
        response.EnsureSuccessStatusCode();
    }
}
