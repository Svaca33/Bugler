// Sends one error log carrying a real exception — type, stack trace and the Runtime that says how
// to read it — to a running Bugler over OTLP/HTTP. What it exists for is the other half of ADR
// 0033: the sample sender declares no exception at all, so its episodes always coarsen to what was
// said, and nothing on the Episodes page ever shows grouping by the code that threw.
//
// Run it twice with two Services' keys of one Application and Environment to watch them meet in one
// Episode with a Participation each (ADR 0034) — the versions differ, the frames do not.
//
// Usage: dotnet run tools/send-thrown-error.cs -- <api-key> [version] [culprit] [endpoint]
#:project ../src/Bugler.Ingestion/Bugler.Ingestion.csproj

using System.Net.Http.Headers;
using Google.Protobuf;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Resource.V1;

var apiKey = args.ElementAtOrDefault(0) ?? throw new ArgumentException("Pass the API key as the first argument.");
var version = args.ElementAtOrDefault(1) ?? "1.4.0";
// The innermost frame of the Application's own code — change it to mint a different kind of trouble.
var culprit = args.ElementAtOrDefault(2) ?? "Acme.Payments.Charge";
var endpoint = args.ElementAtOrDefault(3) ?? "http://127.0.0.1:4318";

var client = new HttpClient { BaseAddress = new Uri(endpoint) };
client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

var now = DateTime.UtcNow;

// The message carries a hostname and a transaction number that differ on every send, and the line
// numbers move — none of it survives the recipe, which is the point worth seeing.
var host = $"db-{Random.Shared.Next(1, 40):00}";
var transaction = Random.Shared.Next(10_000, 99_999);
var line = Random.Shared.Next(20, 90);
var stack = $"""
    System.TimeoutException: connect timed out to {host} (txn {transaction})
       at {culprit}(Order order) in /src/Payments.cs:line {line}
     --- End of stack trace from previous location ---
       at Acme.Checkout.Handler.HandleAsync(Order order) in /src/Checkout/Handler.cs:line {line + 40}
       at Acme.Api.Endpoint.Post(Request request) in /src/Api/Endpoint.cs:line 18
    """;

var resource = new Resource
{
    Attributes =
    {
        new KeyValue { Key = "service.namespace", Value = new AnyValue { StringValue = "aurora" } },
        new KeyValue { Key = "deployment.environment.name", Value = new AnyValue { StringValue = "prod" } },
        new KeyValue { Key = "service.name", Value = new AnyValue { StringValue = "eshop" } },
        // The two attributes ADR 0033 added to the poll: which recipe reads the stack, and what
        // the Participation records as the version this trouble happened on.
        new KeyValue { Key = "telemetry.sdk.language", Value = new AnyValue { StringValue = "dotnet" } },
        new KeyValue { Key = "service.version", Value = new AnyValue { StringValue = version } },
    },
};

var record = new LogRecord
{
    TimeUnixNano = (ulong)(now - DateTime.UnixEpoch).Ticks * 100,
    SeverityNumber = SeverityNumber.Error,
    SeverityText = "ERROR",
    Body = new AnyValue { StringValue = $"Charging order failed after 30000 ms (txn {transaction})" },
    Attributes =
    {
        new KeyValue { Key = "exception.type", Value = new AnyValue { StringValue = "System.TimeoutException" } },
        new KeyValue { Key = "exception.stacktrace", Value = new AnyValue { StringValue = stack } },
        // Serilog's own template attribute — the one the poll was blind to before ADR 0033.
        new KeyValue { Key = "message_template.text", Value = new AnyValue { StringValue = "Charging order failed after {Elapsed} ms (txn {Transaction})" } },
    },
};

var logs = new ExportLogsServiceRequest();
var resourceLogs = new ResourceLogs { Resource = resource };
var scopeLogs = new ScopeLogs { Scope = new InstrumentationScope { Name = "Acme.Payments" } };
scopeLogs.LogRecords.Add(record);
resourceLogs.ScopeLogs.Add(scopeLogs);
logs.ResourceLogs.Add(resourceLogs);

var content = new ByteArrayContent(logs.ToByteArray());
content.Headers.ContentType = new MediaTypeHeaderValue("application/x-protobuf");
var response = await client.PostAsync("/v1/logs", content);
Console.WriteLine($"logs: {(int)response.StatusCode} {response.StatusCode} — {culprit} on {version}");
