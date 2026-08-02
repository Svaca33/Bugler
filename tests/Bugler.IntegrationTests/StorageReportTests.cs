using System.Net;
using System.Net.Http.Json;
using Bugler.Ingestion.ReportStorageFootprint;

namespace Bugler.IntegrationTests;

/// <summary>
/// The storage report prices every registered Service — the loud, the quiet and the silent —
/// and only an Admin may read it.
/// </summary>
public sealed class StorageReportTests : IAsyncLifetime
{
    private BuglerHarness _harness = null!;

    public async Task InitializeAsync() => _harness = await BuglerHarness.StartAsync();

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task Prices_each_service_and_carries_the_retention_its_purge_works_from()
    {
        var loud = _harness.ServiceId;
        var (quiet, _) = await _harness.SeedServiceAsync(_harness.ApplicationId, "acme", "prod", "worker");
        var (silent, _) = await _harness.SeedServiceAsync(_harness.ApplicationId, "acme", "prod", "mobile");

        // The loud Service logged within the measured day; the quiet one holds only older rows —
        // inside its retention, outside the window.
        await _harness.ExecuteSqlAsync($$"""
            INSERT INTO telemetry.log_records
                (service_id, timestamp, severity_number, body, resource_attributes, attributes)
            VALUES
                ('{{loud}}', now() - interval '1 hour', 9, 'recent noise', '{}', '{}'),
                ('{{loud}}', now() - interval '2 hours', 9, 'more recent noise', '{}', '{}'),
                ('{{quiet}}', now() - interval '3 days', 9, 'old news', '{}', '{}')
            """);
        await _harness.ExecuteSqlAsync($$"""
            INSERT INTO telemetry.spans
                (service_id, trace_id, span_id, name, kind, start_time, end_time, status_code,
                 resource_attributes, attributes, events, links)
            VALUES
                ('{{loud}}', 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa', 'aaaaaaaaaaaaaaaa', 'checkout', 0,
                 now() - interval '1 hour', now() - interval '1 hour', 0, '{}', '{}', '[]', '[]')
            """);

        var report = await _harness.Client.GetFromJsonAsync<StorageReportResponse>("/api/admin/storage");

        Assert.NotNull(report);
        Assert.True(report.WindowEnd > report.WindowStart);
        Assert.True(report.LogTableBytes > 0);
        Assert.True(report.TraceTableBytes > 0);

        var byService = report.Services.ToDictionary(s => s.ServiceId);
        Assert.Equal(3, byService.Count);

        var loudLogs = byService[loud].Logs;
        Assert.True(loudLogs.WindowBytes > 0);
        Assert.True(loudLogs.FootprintBytes > 0);
        Assert.True(byService[loud].Traces.WindowBytes > 0);

        var quietLogs = byService[quiet].Logs;
        Assert.Equal(0, quietLogs.WindowBytes);
        Assert.True(quietLogs.FootprintBytes > 0);

        var silentLogs = byService[silent].Logs;
        Assert.Equal(0, silentLogs.FootprintBytes);
        Assert.Equal(0, silentLogs.WindowBytes);

        // The server defaults, resolved the same way the purge resolves them.
        Assert.All(report.Services, s =>
        {
            Assert.Equal(7, s.Logs.RetentionDays);
            Assert.Equal(3, s.Traces.RetentionDays);
        });
    }

    [Fact]
    public async Task The_report_is_read_with_a_capability_the_signed_out_and_the_granted_lack()
    {
        var anonymous = await _harness.CreateAnonymousClient().GetAsync("/api/admin/storage");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var reader = await _harness.CreateUserClientAsync(
            "reader@bugler.test", "ReaderPass123!", _harness.ApplicationId);
        var granted = await reader.GetAsync("/api/admin/storage");
        Assert.Equal(HttpStatusCode.Forbidden, granted.StatusCode);
    }
}
