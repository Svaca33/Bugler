using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bugler.Ingestion.PurgeExpiredTelemetry;
using Bugler.Registry.ManageCatalog;
using Google.Protobuf;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;

namespace Bugler.IntegrationTests;

/// <summary>
/// Deleting a Service or an Application takes everything that belonged to it across every
/// context — telemetry, API keys and read grants — and nothing that did not (ADR 0007).
/// </summary>
public sealed class CatalogDeletionTests : IAsyncLifetime
{
    private BuglerHarness _harness = null!;

    public async Task InitializeAsync() => _harness = await BuglerHarness.StartAsync();

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task Deleting_a_service_erases_its_telemetry_and_keys()
    {
        var service = _harness.ServiceId;
        await SeedLogAsync(service, "gone");
        await SeedSpanAsync(service, trace: "aa", span: "aa", name: "gone-span");

        var deleted = await _harness.Client.DeleteAsync($"/api/admin/services/{service}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        Assert.Equal(0, await _harness.WaitForCountAsync(
            $"SELECT COUNT(*) FROM telemetry.log_records WHERE service_id = '{service}'", 0));
        Assert.Equal(0, await _harness.WaitForCountAsync(
            $"SELECT COUNT(*) FROM telemetry.spans WHERE service_id = '{service}'", 0));

        // The API keys went with the registration, so the Service can no longer export.
        Assert.Equal(0, await _harness.WaitForCountAsync(
            $"SELECT COUNT(*) FROM registry.api_keys WHERE service_id = '{service}'", 0));
        Assert.Equal(HttpStatusCode.Unauthorized, (await IngestAsync(_harness.ApiKey)).StatusCode);

        var remaining = await _harness.Client.GetFromJsonAsync<List<ServiceDto>>(
            $"/api/admin/applications/{_harness.ApplicationId}/services");
        Assert.Empty(remaining!);

        // Delivered messages are dropped: a quiet outbox is the steady state.
        Assert.Equal(0, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM registry.outbox_messages", 0));
    }

    [Fact]
    public async Task Deleting_a_service_is_not_found_twice()
    {
        var first = await _harness.Client.DeleteAsync($"/api/admin/services/{_harness.ServiceId}");
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        var second = await _harness.Client.DeleteAsync($"/api/admin/services/{_harness.ServiceId}");
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
    }

    [Fact]
    public async Task Deleting_a_service_leaves_the_other_spans_of_a_shared_trace()
    {
        var doomed = _harness.ServiceId;
        var (survivor, _) = await _harness.SeedServiceAsync(_harness.ApplicationId, "acme", "prod", "worker");

        const string sharedTrace = "cccccccccccccccccccccccccccccccc";
        await SeedSpanAsync(doomed, sharedTrace, span: "dd", name: "doomed-span");
        await SeedSpanAsync(survivor, sharedTrace, span: "ee", name: "surviving-span");

        var deleted = await _harness.Client.DeleteAsync($"/api/admin/services/{doomed}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        Assert.Equal(0, await _harness.WaitForCountAsync(
            $"SELECT COUNT(*) FROM telemetry.spans WHERE service_id = '{doomed}'", 0));

        // Erasure is bounded by the sender: another Service's spans of the same Trace stay.
        var survived = await _harness.WaitForRowsAsync(
            $"SELECT name FROM telemetry.spans WHERE trace_id = '{sharedTrace}'", expectedCount: 1);
        Assert.Equal("surviving-span", Assert.Single(survived));
    }

    [Fact]
    public async Task Deleting_an_application_removes_its_services_grants_and_telemetry()
    {
        var (applicationId, serviceId, _) = await _harness.SeedApplicationAsync("CRM", "globex", "prod", "backend");
        await _harness.SeedServiceAsync(applicationId, "globex", "prod", "mobile");
        await SeedLogAsync(serviceId, "from CRM");
        await _harness.CreateUserClientAsync("viewer@bugler.test", "ViewerPass123!", applicationId);

        Assert.Equal(1, await _harness.WaitForCountAsync(
            $"SELECT COUNT(*) FROM access.application_grants WHERE application_id = '{applicationId}'", 1));

        var deleted = await _harness.Client.DeleteAsync($"/api/admin/applications/{applicationId}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        Assert.Equal(0, await _harness.WaitForCountAsync(
            $"SELECT COUNT(*) FROM registry.applications WHERE id = '{applicationId}'", 0));
        Assert.Equal(0, await _harness.WaitForCountAsync(
            $"SELECT COUNT(*) FROM registry.services WHERE application_id = '{applicationId}'", 0));
        Assert.Equal(0, await _harness.WaitForCountAsync(
            $"SELECT COUNT(*) FROM telemetry.log_records WHERE service_id = '{serviceId}'", 0));
        Assert.Equal(0, await _harness.WaitForCountAsync(
            $"SELECT COUNT(*) FROM access.application_grants WHERE application_id = '{applicationId}'", 0));

        // The Application seeded by the harness is untouched.
        var applications = await _harness.Client.GetFromJsonAsync<List<ApplicationDto>>("/api/admin/applications");
        Assert.Equal("Eshop", Assert.Single(applications!).Name);
    }

    [Fact]
    public async Task Purge_reclaims_telemetry_of_a_service_that_is_no_longer_registered()
    {
        // What erasure cannot see: Signals written after it ran, or left by a parked message.
        var orphan = Guid.CreateVersion7();
        await SeedLogAsync(orphan, "orphaned");
        await SeedLogAsync(_harness.ServiceId, "kept");

        await _harness.GetRequiredService<TelemetryPurger>().PurgeOnceAsync(CancellationToken.None);

        var remaining = await _harness.WaitForRowsAsync("SELECT body FROM telemetry.log_records", expectedCount: 1);
        Assert.Equal("kept", Assert.Single(remaining));
    }

    private Task SeedLogAsync(Guid serviceId, string body) =>
        _harness.ExecuteSqlAsync($$"""
            INSERT INTO telemetry.log_records
                (service_id, timestamp, severity_number, body, resource_attributes, attributes)
            VALUES ('{{serviceId}}', now() - interval '1 minute', 9, '{{body}}', '{}', '{}')
            """);

    private Task SeedSpanAsync(Guid serviceId, string trace, string span, string name) =>
        _harness.ExecuteSqlAsync($$"""
            INSERT INTO telemetry.spans
                (service_id, trace_id, span_id, name, kind, start_time, end_time, status_code,
                 resource_attributes, attributes, events, links)
            VALUES ('{{serviceId}}', '{{trace.PadRight(32, trace[0])}}', '{{span.PadRight(16, span[0])}}',
                    '{{name}}', 0, now() - interval '1 minute', now() - interval '1 minute', 0,
                    '{}', '{}', '[]', '[]')
            """);

    private async Task<HttpResponseMessage> IngestAsync(string apiKey)
    {
        var request = new ExportLogsServiceRequest();
        var resourceLogs = new ResourceLogs();
        var scopeLogs = new ScopeLogs();
        scopeLogs.LogRecords.Add(new LogRecord
        {
            TimeUnixNano = (ulong)(DateTime.UtcNow - DateTime.UnixEpoch).Ticks * 100,
            SeverityNumber = SeverityNumber.Info,
            Body = new AnyValue { StringValue = "after deletion" },
        });
        resourceLogs.ScopeLogs.Add(scopeLogs);
        request.ResourceLogs.Add(resourceLogs);

        var content = new ByteArrayContent(request.ToByteArray());
        content.Headers.ContentType = new("application/x-protobuf");
        var message = new HttpRequestMessage(HttpMethod.Post, "/v1/logs") { Content = content };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        return await _harness.CreateAnonymousClient().SendAsync(message);
    }
}
