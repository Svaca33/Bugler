using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Xml;
using Bugler.Exploration.GetTraceWaterfall;
using Bugler.Exploration.ListTraces;
using Bugler.Exploration.ObservedKeys;
using Bugler.Exploration.SearchLogs;
using Bugler.Exploration.Querying;
using Bugler.Exploration.SummarizeLogVolume;
using Bugler.Exploration.SummarizeLogVolumeByService;
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
    public async Task A_relative_range_cuts_the_bottom_off_and_leaves_the_top_open()
    {
        var now = DateTime.UtcNow;
        await IngestOneLogAsync(_harness.ApiKey, "Ancient history", now.AddDays(-2));
        await IngestOneLogAsync(_harness.ApiKey, "Clock ran ahead", now.AddHours(1));
        await _harness.WaitForRowsAsync("SELECT body FROM telemetry.log_records", expectedCount: 5);

        var lastDay = await GetAsync<SearchLogsResponse>("/api/logs?range=P1D");
        Assert.DoesNotContain(lastDay.Items, item => item.Body == "Ancient history");
        // The top stays open, so a sender whose clock ran ahead stays visible instead of vanishing.
        Assert.Contains(lastDay.Items, item => item.Body == "Clock ran ahead");

        var window = await GetAsync<SearchLogsResponse>(
            $"/api/logs?from={Instant(now.AddDays(-3))}&to={Instant(now.AddDays(-1))}");
        Assert.Equal("Ancient history", Assert.Single(window.Items).Body);

        var openEnded = await GetAsync<SearchLogsResponse>($"/api/logs?from={Instant(now.AddMinutes(-5))}");
        Assert.Equal(4, openEnded.Items.Count);

        var recentTraces = await GetAsync<ListTracesResponse>("/api/traces?range=PT1H");
        Assert.Single(recentTraces.Items);

        var oldTraces = await GetAsync<ListTracesResponse>($"/api/traces?to={Instant(now.AddDays(-1))}");
        Assert.Empty(oldTraces.Items);
    }

    [Fact]
    public async Task Follow_reads_forward_from_its_cursor_and_narrows_by_the_same_filter()
    {
        var all = await GetAsync<SearchLogsResponse>("/api/logs");
        var newest = all.Items[0];
        var oneBehind = all.Items[1];

        // A reader who is up to date is told nothing rather than told again.
        Assert.Empty((await GetAsync<SearchLogsResponse>(After(newest))).Items);

        // From one behind, only what is newer comes back — never the record the cursor names.
        Assert.Equal(newest.Id, Assert.Single((await GetAsync<SearchLogsResponse>(After(oneBehind))).Items).Id);

        await IngestOneLogAsync(_harness.ApiKey, "Deploy finished");
        await _harness.WaitForRowsAsync("SELECT body FROM telemetry.log_records", expectedCount: 4);

        var arrived = await GetAsync<SearchLogsResponse>(After(newest));
        Assert.Equal("Deploy finished", Assert.Single(arrived.Items).Body);

        // Every other criterion still applies: a tick is the list's Filter read forward, not past it.
        Assert.Empty((await GetAsync<SearchLogsResponse>($"{After(oneBehind)}&namespace=vysocina")).Items);
        Assert.Empty((await GetAsync<SearchLogsResponse>($"{After(oneBehind)}&severityMin=17")).Items);

        // A burst wider than the page yields its newest, not its oldest: the tail stays at the end
        // of the stream instead of falling behind it, and says nothing of what it passed over.
        var capped = await GetAsync<SearchLogsResponse>($"{After(all.Items[^1])}&limit=1");
        Assert.Equal("Deploy finished", Assert.Single(capped.Items).Body);
    }

    [Fact]
    public async Task Volume_counts_the_same_records_the_list_shows_split_by_severity_band()
    {
        var volume = await GetAsync<LogVolumeResponse>("/api/logs/volume?range=PT1H");

        // The sample is 2 Info and 1 Warn, so the bands have to add up to what the list returns.
        Assert.Equal(2, volume.Buckets.Sum(b => b.Info));
        Assert.Equal(1, volume.Buckets.Sum(b => b.Warn));
        Assert.Equal(0, volume.Buckets.Sum(b => b.Error));
        Assert.Equal(0, volume.Buckets.Sum(b => b.Debug));

        var list = await GetAsync<SearchLogsResponse>("/api/logs?range=PT1H");
        Assert.Equal(list.Items.Count, volume.Buckets.Sum(b => b.Error + b.Warn + b.Info + b.Debug));
    }

    [Fact]
    public async Task Volume_reports_the_window_it_resolved_and_fills_every_bucket_in_it()
    {
        var volume = await GetAsync<LogVolumeResponse>("/api/logs/volume?range=PT1H");
        var width = XmlConvert.ToTimeSpan(volume.Bucket);

        Assert.Equal(TimeSpan.FromSeconds(30), width);
        Assert.InRange(volume.To - volume.From, TimeSpan.FromMinutes(59), TimeSpan.FromMinutes(61));

        // Dense: no gap anywhere, so silence cannot collapse into a shorter stretch of time.
        Assert.All(
            volume.Buckets.Zip(volume.Buckets.Skip(1)),
            pair => Assert.Equal(width, pair.Second.Start - pair.First.Start));

        // The Bucket holding `From` opens on or before it — edges sit on multiples of the width.
        Assert.True(volume.Buckets[0].Start <= volume.From);
        Assert.True(volume.Buckets[0].Start > volume.From - width);

        // `To` is stretched to cover the newest record, so the Bucket holding it closes exactly on
        // `To` and reads as finished. Only `Now` tells a viewer that it is in fact still filling.
        Assert.InRange(volume.Now, volume.From, volume.To);
        Assert.True(
            volume.Buckets[^1].Start + width > volume.Now,
            $"the last bucket closed at {volume.Buckets[^1].Start + width:O}, before now {volume.Now:O}");
    }

    [Fact]
    public async Task Volume_stretches_past_now_for_a_sender_whose_clock_ran_ahead()
    {
        var now = DateTime.UtcNow;
        await IngestOneLogAsync(_harness.ApiKey, "Clock ran ahead", now.AddHours(1));
        await _harness.WaitForRowsAsync("SELECT body FROM telemetry.log_records", expectedCount: 4);

        var volume = await GetAsync<LogVolumeResponse>("/api/logs/volume?range=PT1H");

        // A window capped at now would hide the anomaly the list deliberately shows (ADR 0002).
        Assert.True(volume.To > now.AddMinutes(30), $"window ended at {volume.To:O}, before the future record");
        // The window then reaches past the clock, which is the one case where the Buckets at the top
        // are neither filling nor cut short — they are already whole and simply lie ahead of now.
        Assert.True(volume.To > volume.Now);
        Assert.Equal(4, volume.Buckets.Sum(b => b.Error + b.Warn + b.Info + b.Debug));
    }

    [Fact]
    public async Task Volume_of_an_unbounded_filter_opens_at_the_oldest_matching_record()
    {
        var now = DateTime.UtcNow;
        await IngestOneLogAsync(_harness.ApiKey, "Ancient history", now.AddDays(-2));
        await _harness.WaitForRowsAsync("SELECT body FROM telemetry.log_records", expectedCount: 4);

        var volume = await GetAsync<LogVolumeResponse>("/api/logs/volume");

        Assert.True(volume.From <= now.AddDays(-2), $"window opened at {volume.From:O}, after the oldest record");
        Assert.Equal(4, volume.Buckets.Sum(b => b.Error + b.Warn + b.Info + b.Debug));
    }

    [Fact]
    public async Task Volume_and_the_total_narrow_by_everything_the_list_narrows_by()
    {
        var warnOnly = await GetAsync<LogVolumeResponse>("/api/logs/volume?range=PT1H&severityMin=13");
        Assert.Equal(1, warnOnly.Buckets.Sum(b => b.Warn));
        Assert.Equal(0, warnOnly.Buckets.Sum(b => b.Info));

        var byTenant = await GetAsync<LogVolumeResponse>($"/api/logs/volume?attr={FilterJson("acme", "tenant.id")}");
        Assert.Equal(2, byTenant.Buckets.Sum(b => b.Error + b.Warn + b.Info + b.Debug));

        Assert.Equal(3, (await GetAsync<LogCountResponse>("/api/logs/count")).Total);
        Assert.Equal(1, (await GetAsync<LogCountResponse>("/api/logs/count?severityMin=13")).Total);
        Assert.Equal(2, (await GetAsync<LogCountResponse>($"/api/logs/count?attr={FilterJson("acme", "tenant.id")}")).Total);
        Assert.Equal(0, (await GetAsync<LogCountResponse>("/api/logs/count?namespace=vysocina")).Total);
    }

    [Fact]
    public async Task Volume_by_service_keeps_services_apart_and_shares_one_axis()
    {
        var (workerId, workerKey) = await _harness.SeedServiceAsync(
            _harness.ApplicationId, "acme", "prod", "worker");
        await IngestOneLogAsync(workerKey, "Queue jammed", severity: SeverityNumber.Error);
        await _harness.WaitForRowsAsync("SELECT body FROM telemetry.log_records", expectedCount: 4);

        var volume = await GetAsync<LogVolumeByServiceResponse>("/api/logs/volume/by-service?range=PT1H");

        Assert.Equal(2, volume.Services.Count);
        var web = volume.Services.Single(s => s.ServiceId == _harness.ServiceId);
        var worker = volume.Services.Single(s => s.ServiceId == workerId);

        // The sample is 2 Info + 1 Warn on web; the worker's error must not leak into web's bands.
        Assert.Equal((0, 1, 2), (web.Error, web.Warn, web.Info));
        Assert.Equal((1, 0, 0), (worker.Error, worker.Warn, worker.Info));

        // One shared axis: same count, same edges — that is what makes two entries comparable.
        Assert.Equal(web.Buckets.Count, worker.Buckets.Count);
        Assert.All(web.Buckets.Zip(worker.Buckets), pair => Assert.Equal(pair.First.Start, pair.Second.Start));

        // PT1H at the default 24 asked-for Buckets: 2.5 min rounds up to the 5 min rung.
        Assert.Equal(TimeSpan.FromMinutes(5), XmlConvert.ToTimeSpan(volume.Bucket));
        Assert.InRange(web.Buckets.Count, 12, 13);

        // Asking for fewer Buckets widens the rung; the clamp floors a pathological ask at 8.
        var coarse = await GetAsync<LogVolumeByServiceResponse>(
            "/api/logs/volume/by-service?range=PT1H&buckets=8");
        Assert.Equal(TimeSpan.FromMinutes(15), XmlConvert.ToTimeSpan(coarse.Bucket));
        var clamped = await GetAsync<LogVolumeByServiceResponse>(
            "/api/logs/volume/by-service?range=PT1H&buckets=1");
        Assert.Equal(TimeSpan.FromMinutes(15), XmlConvert.ToTimeSpan(clamped.Bucket));
    }

    [Fact]
    public async Task Volume_by_service_shows_a_stranger_nothing_rather_than_someone_elses_services()
    {
        var stranger = await _harness.CreateUserClientAsync("stranger@bugler.test", "Stranger123!");

        var response = await stranger.GetFromJsonAsync<LogVolumeByServiceResponse>(
            "/api/logs/volume/by-service?range=PT1H", Json);

        Assert.Empty(response!.Services);
    }

    [Fact]
    public async Task Impossible_time_filters_are_refused_instead_of_returning_nothing()
    {
        await AssertBadRequestAsync("/api/logs/volume?range=PT1H&from=2026-07-28T09:00:00Z");
        await AssertBadRequestAsync("/api/logs/volume?from=2026-07-28T18:00:00Z&to=2026-07-28T09:00:00Z");
        await AssertBadRequestAsync("/api/logs/count?range=15m");

        await AssertBadRequestAsync("/api/logs?range=PT1H&from=2026-07-28T09:00:00Z");
        await AssertBadRequestAsync("/api/logs?from=2026-07-28T18:00:00Z&to=2026-07-28T09:00:00Z");
        await AssertBadRequestAsync("/api/logs?from=2026-07-28T18:00:00");
        await AssertBadRequestAsync("/api/logs?range=15m");
        await AssertBadRequestAsync("/api/traces?range=PT1H&to=2026-07-28T09:00:00Z");
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

    private static string After(LogRecordDto record) =>
        $"/api/logs?after={Instant(record.Timestamp)}&afterId={record.Id}";

    private static string Instant(DateTime utc) =>
        Uri.EscapeDataString(DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToString("O"));

    private async Task AssertBadRequestAsync(string url)
    {
        var response = await _harness.Client.GetAsync(url);
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task IngestOneLogAsync(
        string apiKey, string body, DateTime? at = null, SeverityNumber severity = SeverityNumber.Info)
    {
        var logs = new ExportLogsServiceRequest();
        var resourceLogs = new ResourceLogs();
        var scopeLogs = new ScopeLogs();
        scopeLogs.LogRecords.Add(new LogRecord
        {
            TimeUnixNano = ToNano(at ?? DateTime.UtcNow),
            SeverityNumber = severity,
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
