using Bugler.Ingestion.PurgeExpiredTelemetry;

namespace Bugler.IntegrationTests;

/// <summary>Purge removes Signals older than the Service's effective retention and nothing else.</summary>
public sealed class RetentionPurgeTests : IAsyncLifetime
{
    private BuglerHarness _harness = null!;

    public async Task InitializeAsync() => _harness = await BuglerHarness.StartAsync();

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task Purge_removes_only_expired_telemetry()
    {
        var service = _harness.ServiceId;
        await _harness.ExecuteSqlAsync($$"""
            INSERT INTO telemetry.log_records
                (service_id, timestamp, severity_number, body, resource_attributes, attributes)
            VALUES
                ('{{service}}', now() - interval '40 days', 9, 'ancient', '{}', '{}'),
                ('{{service}}', now() - interval '1 hour', 9, 'fresh', '{}', '{}')
            """);
        await _harness.ExecuteSqlAsync($$"""
            INSERT INTO telemetry.spans
                (service_id, trace_id, span_id, name, kind, start_time, end_time, status_code,
                 resource_attributes, attributes, events, links)
            VALUES
                ('{{service}}', 'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa', 'aaaaaaaaaaaaaaaa', 'ancient-span', 0,
                 now() - interval '40 days', now() - interval '40 days', 0, '{}', '{}', '[]', '[]'),
                ('{{service}}', 'bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb', 'bbbbbbbbbbbbbbbb', 'fresh-span', 0,
                 now() - interval '1 hour', now() - interval '1 hour', 0, '{}', '{}', '[]', '[]')
            """);

        await _harness.GetRequiredService<TelemetryPurger>().PurgeOnceAsync(CancellationToken.None);

        var logs = await _harness.WaitForRowsAsync("SELECT body FROM telemetry.log_records", expectedCount: 1);
        Assert.Equal("fresh", Assert.Single(logs));
        var spans = await _harness.WaitForRowsAsync("SELECT name FROM telemetry.spans", expectedCount: 1);
        Assert.Equal("fresh-span", Assert.Single(spans));
    }
}
