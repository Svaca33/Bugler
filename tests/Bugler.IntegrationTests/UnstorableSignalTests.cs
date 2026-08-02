using System.Net;
using System.Net.Http.Headers;
using Bugler.Ingestion.Storage;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Resource.V1;

namespace Bugler.IntegrationTests;

/// <summary>
/// What PostgreSQL will not hold, against a real PostgreSQL — the only place either half of this
/// can be shown. A mapper test proves a string was cleaned, not that the database then accepts it;
/// and a Batch surviving one refused Signal is a claim about what the database does with a COPY.
/// </summary>
public sealed class UnstorableSignalTests : IAsyncLifetime
{
    private BuglerHarness _harness = null!;

    public async Task InitializeAsync() => _harness = await BuglerHarness.StartAsync();

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    /// <summary>
    /// The whole of the reported bug: a Service logging a NUL used to take every other Service's
    /// Signals down with it, because the buffer is one channel per signal kind and the Batch drawn
    /// from it is one COPY.
    /// </summary>
    [Fact]
    public async Task A_nul_from_one_service_costs_neither_its_own_signals_nor_anyone_elses()
    {
        var (_, cleanKey) = await _harness.SeedServiceAsync(_harness.ApplicationId, "acme", "prod", "worker");

        var offending = await PostLogAsync(_harness.ApiKey, "Connection reset\0", "payload\0value");
        var clean = await PostLogAsync(cleanKey, "All is well", "ordinary");

        Assert.Equal(HttpStatusCode.OK, offending.StatusCode);
        Assert.Equal(HttpStatusCode.OK, clean.StatusCode);

        var bodies = await _harness.WaitForRowsAsync(
            "SELECT body FROM telemetry.log_records ORDER BY id", expectedCount: 2);

        // The offending Signal is stored, saying a character was there that could not be kept —
        // not quietly rewritten into something the sender never sent.
        Assert.Contains("Connection reset�", bodies);
        Assert.Contains("All is well", bodies);

        var payloads = await _harness.WaitForRowsAsync(
            "SELECT attributes->>'payload' FROM telemetry.log_records " +
            "WHERE attributes->>'payload' IS NOT NULL ORDER BY id", expectedCount: 2);
        Assert.Contains("payload�value", payloads);
        Assert.Contains("ordinary", payloads);
    }

    /// <summary>
    /// The seatbelt, driven directly: taming NUL closes the refusal we know about, and this is the
    /// one we do not. Driven through the buffer instead, the batching would be a race — the writer
    /// decides how many rows one Batch holds.
    /// </summary>
    [Fact]
    public async Task One_signal_postgresql_refuses_no_longer_costs_the_rest_of_its_batch()
    {
        var batch = new List<LogRecordRow>
        {
            Row("first"),
            Row("second"),
            Unstorable("third"),
            Row("fourth"),
            Row("fifth"),
        };

        await Importer().ImportAsync(batch, CancellationToken.None);

        var bodies = await _harness.WaitForRowsAsync(
            "SELECT body FROM telemetry.log_records ORDER BY id", expectedCount: 4);

        Assert.Equal(["first", "second", "fourth", "fifth"], bodies);
    }

    /// <summary>
    /// The other end of the same path: when nothing in the Batch can be stored, the Salvage costs
    /// its attempts, stores nothing, and still returns — a refusal is never allowed to take the
    /// writer's loop down with it.
    /// </summary>
    [Fact]
    public async Task A_batch_of_nothing_but_refusals_stores_nothing_and_still_returns()
    {
        var batch = new List<LogRecordRow>
        {
            Unstorable("first"), Unstorable("second"), Unstorable("third"), Unstorable("fourth"),
        };

        await Importer().ImportAsync(batch, CancellationToken.None);

        Assert.Equal(0, await _harness.WaitForCountAsync("SELECT count(*) FROM telemetry.log_records", expected: 0));
    }

    private BatchImporter<LogRecordRow> Importer() => new(
        _harness.GetRequiredService<NpgsqlDataSource>(),
        LogWriter.CopyCommand,
        LogWriter.WriteRowAsync,
        "log records",
        _harness.GetRequiredService<ILoggerFactory>().CreateLogger<BatchImporter<LogRecordRow>>());

    private LogRecordRow Row(string body) => new(
        _harness.ServiceId, DateTime.UtcNow, null, 9, "INFO", body, null, null, null, "{}", "{}");

    /// <summary>
    /// A row PostgreSQL refuses for a reason the mapper does not know about — jsonb that is not
    /// JSON. Any such reason would do; the point is that it is not the one we just fixed.
    /// </summary>
    private LogRecordRow Unstorable(string body) => Row(body) with { Attributes = "{" };

    private async Task<HttpResponseMessage> PostLogAsync(string apiKey, string body, string payload)
    {
        var scopeLogs = new ScopeLogs();
        scopeLogs.LogRecords.Add(new LogRecord
        {
            TimeUnixNano = (ulong)(DateTime.UtcNow - DateTime.UnixEpoch).Ticks * 100,
            SeverityNumber = SeverityNumber.Error,
            Body = new AnyValue { StringValue = body },
            Attributes = { new KeyValue { Key = "payload", Value = new AnyValue { StringValue = payload } } },
        });

        var resourceLogs = new ResourceLogs
        {
            Resource = new Resource
            {
                Attributes =
                {
                    new KeyValue { Key = "service.name", Value = new AnyValue { StringValue = "web" } },
                },
            },
        };
        resourceLogs.ScopeLogs.Add(scopeLogs);
        var request = new ExportLogsServiceRequest();
        request.ResourceLogs.Add(resourceLogs);

        var content = new ByteArrayContent(request.ToByteArray());
        content.Headers.ContentType = new("application/x-protobuf");
        var message = new HttpRequestMessage(HttpMethod.Post, "/v1/logs") { Content = content };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        return await _harness.Client.SendAsync(message);
    }
}
