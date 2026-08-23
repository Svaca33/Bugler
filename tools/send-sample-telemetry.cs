// Sends sample logs and a trace to a running Bugler over OTLP/HTTP.
//
// The error log carries a real exception - type, stack trace, and the Runtime that says how to
// read it - because a real .NET sender does, and because without one every Episode it opens falls
// to the bottom rung of the ladder and reads as coarsened (ADR 0033). --culprit changes the
// innermost frame, which is what tells one kind of trouble from another; --version changes what
// the Participation records. Two runs with two Services' keys of one Application and Environment,
// the same culprit and different versions, put both Services in one Episode (ADR 0034).
//
// Usage: dotnet run tools/send-sample-telemetry.cs -- <api-key> [endpoint]
//                   [--culprit Acme.Payments.Charge] [--version 1.4.0]
#:project ../src/Bugler.Ingestion/Bugler.Ingestion.csproj

using System.Net.Http.Headers;
using Google.Protobuf;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;

// The two optional flags take a value each, so the positional arguments are what is left once
// both a flag and the word after it have been stepped over.
string[] flagNames = ["--culprit", "--version"];
var positional = new List<string>();
for (var i = 0; i < args.Length; i++)
{
    if (flagNames.Contains(args[i]))
    {
        i++;
        continue;
    }

    positional.Add(args[i]);
}

var apiKey = positional.ElementAtOrDefault(0)
    ?? throw new ArgumentException("Pass the API key as the first argument.");
var endpoint = positional.ElementAtOrDefault(1) ?? "http://127.0.0.1:4318";
var culprit = Flag("--culprit") ?? "Acme.Payments.Charge";
var version = Flag("--version") ?? "1.4.0";

string? Flag(string name)
{
    var at = Array.IndexOf(args, name);
    return at >= 0 && at + 1 < args.Length ? args[at + 1] : null;
}

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
        // What the Fingerprint recipe reads the stack with, and what a Participation records
        // as the version this trouble happened on.
        new KeyValue { Key = "telemetry.sdk.language", Value = new AnyValue { StringValue = "dotnet" } },
        new KeyValue { Key = "service.version", Value = new AnyValue { StringValue = version } },
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
// The exception the log reports. Everything that varies here - the host, the transaction, the
// line numbers - varies on every run and none of it survives the recipe, so two runs meet in one
// Episode while a different --culprit opens its own.
errorLog.Attributes.Add(new KeyValue
{
    Key = "exception.type",
    Value = new AnyValue { StringValue = "Acme.Payments.PaymentDeclinedException" },
});
errorLog.Attributes.Add(new KeyValue
{
    Key = "exception.stacktrace",
    Value = new AnyValue { StringValue = Stack(culprit) },
});
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

// A .NET stack trace as the runtime writes one: a header carrying the exception's own message,
// frames with their source locations, and the async marker between them.
static string Stack(string culprit)
{
    var host = $"db-{Random.Shared.Next(1, 40):00}";
    var transaction = Random.Shared.Next(10_000, 99_999);
    var line = Random.Shared.Next(20, 90);
    return $"""
        Acme.Payments.PaymentDeclinedException: card declined by {host} (txn {transaction})
           at {culprit}(Order order) in /src/Payments.cs:line {line}
         --- End of stack trace from previous location ---
           at Acme.Checkout.Handler.HandleAsync(Order order) in /src/Checkout/Handler.cs:line {line + 40}
           at Acme.Api.Endpoint.Post(Request request) in /src/Api/Endpoint.cs:line 18
        """;
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
