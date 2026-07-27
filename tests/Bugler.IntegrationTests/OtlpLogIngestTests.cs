using System.Net;
using System.Net.Http.Headers;
using Bugler.Registry.Catalog;
using Google.Protobuf;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Resource.V1;

namespace Bugler.IntegrationTests;

/// <summary>
/// Whole log write path against a real PostgreSQL: authenticate, ingest via OTLP/HTTP,
/// and observe the rows the background writer persisted.
/// </summary>
public sealed class OtlpLogIngestTests : IAsyncLifetime
{
    private BuglerHarness _harness = null!;

    public async Task InitializeAsync() => _harness = await BuglerHarness.StartAsync();

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task Export_with_valid_key_persists_log_records()
    {
        var response = await PostLogsAsync(_harness.ApiKey, "First things first", "And then the rest");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var bodies = await _harness.WaitForRowsAsync(
            "SELECT body FROM telemetry.log_records ORDER BY id", expectedCount: 2);
        Assert.Contains("First things first", bodies);
        Assert.Contains("And then the rest", bodies);
    }

    [Fact]
    public async Task Export_with_unknown_key_is_rejected()
    {
        var response = await PostLogsAsync(ApiKeyMaterial.GeneratePlaintext(), "Should never land");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<HttpResponseMessage> PostLogsAsync(string apiKey, params string[] bodies)
    {
        var scopeLogs = new ScopeLogs();
        foreach (var body in bodies)
        {
            scopeLogs.LogRecords.Add(new LogRecord
            {
                TimeUnixNano = (ulong)(DateTime.UtcNow - DateTime.UnixEpoch).Ticks * 100,
                SeverityNumber = SeverityNumber.Info,
                Body = new AnyValue { StringValue = body },
            });
        }

        var request = new ExportLogsServiceRequest();
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
        request.ResourceLogs.Add(resourceLogs);

        var content = new ByteArrayContent(request.ToByteArray());
        content.Headers.ContentType = new("application/x-protobuf");
        var message = new HttpRequestMessage(HttpMethod.Post, "/v1/logs") { Content = content };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        return await _harness.Client.SendAsync(message);
    }
}
