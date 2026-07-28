// Sends sample logs and a trace to a running Bugler over OTLP/HTTP.
// Usage: dotnet run tools/send-sample-telemetry.cs -- <api-key> [endpoint]
#:project ../src/Bugler.Ingestion/Bugler.Ingestion.csproj

using System.Net.Http.Headers;
using Google.Protobuf;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;

var apiKey = args.ElementAtOrDefault(0) ?? throw new ArgumentException("Pass the API key as the first argument.");
var endpoint = args.ElementAtOrDefault(1) ?? "http://127.0.0.1:4318";

var client = new HttpClient { BaseAddress = new Uri(endpoint) };
client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

var now = DateTime.UtcNow;
var traceId = ByteString.CopyFrom(Guid.NewGuid().ToByteArray());
var rootSpanId = ByteString.CopyFrom(Guid.NewGuid().ToByteArray()[..8]);
var childSpanId = ByteString.CopyFrom(Guid.NewGuid().ToByteArray()[..8]);

// What the payload declares about itself. Bugler files it under the Service the API key
// proves and keeps these as ordinary resource attributes (ADR 0006).
var resource = new Resource
{
    Attributes =
    {
        new KeyValue { Key = "service.namespace", Value = new AnyValue { StringValue = "demo" } },
        new KeyValue { Key = "deployment.environment.name", Value = new AnyValue { StringValue = "production" } },
        new KeyValue { Key = "service.name", Value = new AnyValue { StringValue = "eshop-web" } },
        new KeyValue { Key = "service.instance.id", Value = new AnyValue { StringValue = Environment.MachineName } },
    },
};

var logs = new ExportLogsServiceRequest();
var resourceLogs = new ResourceLogs { Resource = resource };
var scopeLogs = new ScopeLogs { Scope = new InstrumentationScope { Name = "Eshop.Checkout" } };
scopeLogs.LogRecords.Add(Log(now.AddSeconds(-5), SeverityNumber.Info, "Order 1042 placed by customer", "acme"));
scopeLogs.LogRecords.Add(Log(now.AddSeconds(-4), SeverityNumber.Info, "Inventory reserved for order 1042", "acme"));
scopeLogs.LogRecords.Add(Log(now.AddSeconds(-3), SeverityNumber.Warn, "Payment gateway slow (1900 ms)", "globex"));
var errorLog = Log(now.AddSeconds(-2), SeverityNumber.Error, "Payment declined: insufficient funds", "acme");
errorLog.TraceId = traceId;
errorLog.SpanId = childSpanId;
scopeLogs.LogRecords.Add(errorLog);
resourceLogs.ScopeLogs.Add(scopeLogs);
logs.ResourceLogs.Add(resourceLogs);

var traces = new ExportTraceServiceRequest();
var resourceSpans = new ResourceSpans { Resource = resource };
var scopeSpans = new ScopeSpans { Scope = new InstrumentationScope { Name = "Eshop.Checkout" } };
scopeSpans.Spans.Add(new Span
{
    TraceId = traceId,
    SpanId = rootSpanId,
    Name = "POST /checkout",
    Kind = Span.Types.SpanKind.Server,
    StartTimeUnixNano = Nano(now.AddMilliseconds(-2200)),
    EndTimeUnixNano = Nano(now.AddMilliseconds(-100)),
    Status = new Status { Code = Status.Types.StatusCode.Error, Message = "payment declined" },
    Attributes = { new KeyValue { Key = "http.route", Value = new AnyValue { StringValue = "/checkout" } } },
});
scopeSpans.Spans.Add(new Span
{
    TraceId = traceId,
    SpanId = childSpanId,
    ParentSpanId = rootSpanId,
    Name = "charge-card",
    Kind = Span.Types.SpanKind.Client,
    StartTimeUnixNano = Nano(now.AddMilliseconds(-2100)),
    EndTimeUnixNano = Nano(now.AddMilliseconds(-200)),
    Status = new Status { Code = Status.Types.StatusCode.Error, Message = "insufficient funds" },
    Events =
    {
        new Span.Types.Event
        {
            Name = "exception",
            TimeUnixNano = Nano(now.AddMilliseconds(-250)),
            Attributes =
            {
                new KeyValue { Key = "exception.type", Value = new AnyValue { StringValue = "PaymentDeclined" } },
            },
        },
    },
});
resourceSpans.ScopeSpans.Add(scopeSpans);
traces.ResourceSpans.Add(resourceSpans);

Console.WriteLine($"logs:   {await PostAsync("/v1/logs", logs.ToByteArray())}");
Console.WriteLine($"traces: {await PostAsync("/v1/traces", traces.ToByteArray())}");
Console.WriteLine($"trace id: {Convert.ToHexStringLower(traceId.Span)}");

async Task<string> PostAsync(string path, byte[] payload)
{
    var content = new ByteArrayContent(payload);
    content.Headers.ContentType = new MediaTypeHeaderValue("application/x-protobuf");
    var response = await client.PostAsync(path, content);
    return $"{(int)response.StatusCode} {response.StatusCode}";
}

static LogRecord Log(DateTime time, SeverityNumber severity, string body, string tenant) => new()
{
    TimeUnixNano = Nano(time),
    SeverityNumber = severity,
    SeverityText = severity.ToString().ToUpperInvariant().Replace("SEVERITY_NUMBER_", ""),
    Body = new AnyValue { StringValue = body },
    Attributes = { new KeyValue { Key = "tenant.id", Value = new AnyValue { StringValue = tenant } } },
};

static ulong Nano(DateTime utc) => (ulong)(utc - DateTime.UnixEpoch).Ticks * 100;
