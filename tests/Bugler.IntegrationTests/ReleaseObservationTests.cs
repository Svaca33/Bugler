using System.Net.Http.Headers;
using System.Net.Http.Json;
using Google.Protobuf;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Resource.V1;

namespace Bugler.IntegrationTests;

/// <summary>
/// Releases end to end: exported over OTLP, recorded by the write path, read back through
/// Exploration (ADR 0016).
/// </summary>
public sealed class ReleaseObservationTests : IAsyncLifetime
{
    private BuglerHarness _harness = null!;

    public async Task InitializeAsync() => _harness = await BuglerHarness.StartAsync();

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task A_changed_declared_version_is_recorded_as_a_release()
    {
        await ExportAsync("1.2.3");
        await _harness.WaitForCountAsync("SELECT count(*) FROM telemetry.releases", 1);

        await ExportAsync("1.2.4");
        await _harness.WaitForCountAsync("SELECT count(*) FROM telemetry.releases", 2);

        var versions = await _harness.WaitForRowsAsync(
            "SELECT version || ' after ' || COALESCE(previous_version, '-') "
            + "FROM telemetry.releases ORDER BY id", expectedCount: 2);

        // The first row says only what the Service was already running; the second is the Release.
        Assert.Equal(["1.2.3 after -", "1.2.4 after 1.2.3"], versions);
    }

    [Fact]
    public async Task Exporting_the_same_version_again_records_nothing_more()
    {
        await ExportAsync("1.2.3");
        await _harness.WaitForCountAsync("SELECT count(*) FROM telemetry.releases", 1);

        await ExportAsync("1.2.3");
        await ExportAsync("1.2.3");

        Assert.Equal(1, await _harness.WaitForCountAsync("SELECT count(*) FROM telemetry.releases", 1));
    }

    [Fact]
    public async Task A_straggler_of_the_version_just_replaced_is_not_a_second_release()
    {
        await ExportAsync("1.2.3");
        await _harness.WaitForCountAsync("SELECT count(*) FROM telemetry.releases", 1);
        await ExportAsync("1.2.4");
        await _harness.WaitForCountAsync("SELECT count(*) FROM telemetry.releases", 2);

        // A replica that has not restarted yet, still reporting the old version.
        await ExportAsync("1.2.3");

        Assert.Equal(2, await _harness.WaitForCountAsync("SELECT count(*) FROM telemetry.releases", 2));
    }

    [Fact]
    public async Task A_service_declaring_no_version_records_nothing()
    {
        await ExportAsync(version: null);

        await _harness.WaitForRowsAsync(
            "SELECT body FROM telemetry.log_records", expectedCount: 1);
        Assert.Equal(0, await _harness.WaitForCountAsync("SELECT count(*) FROM telemetry.releases", 0));
    }

    [Fact]
    public async Task The_api_answers_with_the_release_and_the_version_the_window_opened_on()
    {
        await ExportAsync("1.2.3");
        await _harness.WaitForCountAsync("SELECT count(*) FROM telemetry.releases", 1);
        await ExportAsync("1.2.4");
        await _harness.WaitForCountAsync("SELECT count(*) FROM telemetry.releases", 2);

        var response = await _harness.Client.GetFromJsonAsync<ReleasesPayload>("/api/releases?range=PT1H");

        var release = Assert.Single(response!.Releases);
        Assert.Equal("1.2.4", release.Version);
        Assert.Equal("1.2.3", release.PreviousVersion);
        Assert.Equal(_harness.ServiceId, release.ServiceId);

        // Both rows sit inside the window here, so nothing preceded it — the baseline is not
        // reported as running before a window it falls inside.
        Assert.Empty(response.Running);
    }

    [Fact]
    public async Task A_window_after_the_release_reports_it_as_the_version_running()
    {
        await ExportAsync("1.2.3");
        await _harness.WaitForCountAsync("SELECT count(*) FROM telemetry.releases", 1);
        await ExportAsync("1.2.4");
        await _harness.WaitForCountAsync("SELECT count(*) FROM telemetry.releases", 2);

        // The Signals are stamped an hour back, so a window of the last minute opens after both.
        var response = await _harness.Client.GetFromJsonAsync<ReleasesPayload>("/api/releases?range=PT1M");

        Assert.Empty(response!.Releases);
        var running = Assert.Single(response.Running);
        Assert.Equal("1.2.4", running.Version);
    }

    [Fact]
    public async Task Releases_of_a_service_outside_the_source_filter_are_not_answered_for()
    {
        await ExportAsync("1.2.3");
        await _harness.WaitForCountAsync("SELECT count(*) FROM telemetry.releases", 1);

        var response = await _harness.Client.GetFromJsonAsync<ReleasesPayload>(
            "/api/releases?range=PT1H&service=nobody-sends-this");

        Assert.Empty(response!.Releases);
        Assert.Empty(response.Running);
    }

    [Fact]
    public async Task Deleting_a_service_takes_its_releases_with_it()
    {
        await ExportAsync("1.2.3");
        await _harness.WaitForCountAsync("SELECT count(*) FROM telemetry.releases", 1);

        var deleted = await _harness.Client.DeleteAsync($"/api/admin/applications/{_harness.ApplicationId}");
        deleted.EnsureSuccessStatusCode();

        Assert.Equal(0, await _harness.WaitForCountAsync("SELECT count(*) FROM telemetry.releases", 0));
    }

    /// <summary>
    /// One export stamped an hour back, so every test's Signals sit inside a one-hour window and
    /// outside a one-minute one whatever the wall clock does while the suite runs.
    /// </summary>
    private async Task ExportAsync(string? version)
    {
        var stamp = DateTime.UtcNow.AddMinutes(-59);
        var scopeLogs = new ScopeLogs();
        scopeLogs.LogRecords.Add(new LogRecord
        {
            TimeUnixNano = (ulong)(stamp - DateTime.UnixEpoch).Ticks * 100,
            SeverityNumber = SeverityNumber.Info,
            Body = new AnyValue { StringValue = $"running {version ?? "unstated"}" },
        });

        var resource = new Resource
        {
            Attributes =
            {
                new KeyValue { Key = "service.name", Value = new AnyValue { StringValue = "web" } },
            },
        };

        if (version is not null)
        {
            resource.Attributes.Add(new KeyValue
            {
                Key = "service.version",
                Value = new AnyValue { StringValue = version },
            });
        }

        var resourceLogs = new ResourceLogs { Resource = resource };
        resourceLogs.ScopeLogs.Add(scopeLogs);
        var request = new ExportLogsServiceRequest();
        request.ResourceLogs.Add(resourceLogs);

        var content = new ByteArrayContent(request.ToByteArray());
        content.Headers.ContentType = new("application/x-protobuf");
        var message = new HttpRequestMessage(HttpMethod.Post, "/v1/logs") { Content = content };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _harness.ApiKey);
        (await _harness.Client.SendAsync(message)).EnsureSuccessStatusCode();
    }

    private sealed record ReleasesPayload(
        List<RunningPayload> Running,
        List<ReleasePayload> Releases);

    private sealed record RunningPayload(Guid ServiceId, string Version, DateTime Since);

    private sealed record ReleasePayload(
        Guid ServiceId, string Version, string PreviousVersion, DateTime At);
}
