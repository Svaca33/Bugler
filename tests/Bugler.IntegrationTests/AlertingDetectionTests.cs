using System.Net.Http.Json;
using Bugler.Alerting.DetectEpisodes;
using Microsoft.AspNetCore.Hosting;

namespace Bugler.IntegrationTests;

public sealed class AlertingDetectionTests : IAsyncLifetime
{
    private BuglerHarness _harness = null!;
    private EpisodeDetector _detector = null!;

    public async Task InitializeAsync()
    {
        // Configured SMTP turns the mail channel on, so detection owes mail Deliveries.
        _harness = await BuglerHarness.StartAsync(builder =>
        {
            builder.UseSetting("Alerting:Smtp:Host", "smtp.test");
            builder.UseSetting("Alerting:Smtp:From", "bugler@test.local");
        });
        _detector = _harness.GetRequiredService<EpisodeDetector>();
    }

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task The_first_matching_log_opens_an_episode_and_owes_the_alerts()
    {
        await _harness.Client.PutAsJsonAsync("/api/alerting/subscriptions", new
        {
            applicationIds = new[] { _harness.ApplicationId },
            serviceIds = Array.Empty<Guid>(),
        });
        await _harness.Client.PutAsJsonAsync(
            $"/api/admin/applications/{_harness.ApplicationId}/alerting/webhook",
            new { url = "https://chat.googleapis.com/v1/spaces/AAA/messages" });

        await _detector.DetectOnceAsync(CancellationToken.None); // Seeds the cursor.

        await _harness.ExecuteSqlAsync($$"""
            INSERT INTO telemetry.log_records
                (service_id, timestamp, severity_number, body, resource_attributes, attributes)
            VALUES ('{{_harness.ServiceId}}', now(), 17, 'boom', '{}', '{}')
            """);
        await _detector.DetectOnceAsync(CancellationToken.None);

        Assert.Equal(1, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.episodes WHERE closed_at IS NULL "
            + "AND error_count = 1 AND warn_count = 0 AND first_log_body = 'boom'", 1));
        Assert.Equal(1, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.deliveries WHERE kind = 1 AND channel = 1", 1));
        Assert.Equal(1, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.deliveries WHERE kind = 1 AND channel = 2", 1));

        // More trouble while open: counted, never re-announced. The warning stays outside
        // the default Errors sensitivity.
        await _harness.ExecuteSqlAsync($$"""
            INSERT INTO telemetry.log_records
                (service_id, timestamp, severity_number, body, resource_attributes, attributes)
            VALUES
                ('{{_harness.ServiceId}}', now(), 21, 'fatal', '{}', '{}'),
                ('{{_harness.ServiceId}}', now(), 14, 'warned', '{}', '{}')
            """);
        await _detector.DetectOnceAsync(CancellationToken.None);

        Assert.Equal(1, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.episodes WHERE error_count = 2 AND warn_count = 0", 1));
        Assert.Equal(2, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.deliveries", 2));
    }

    [Fact]
    public async Task An_out_of_order_commit_inside_the_overlap_is_still_counted()
    {
        await _detector.DetectOnceAsync(CancellationToken.None); // Seeds the cursor at 0.

        await InsertWithIdAsync(20_000, "first");
        await _detector.DetectOnceAsync(CancellationToken.None);
        Assert.Equal(1, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.episodes WHERE error_count = 1", 1));

        // A transaction holding a lower id committed after the poll passed it: the overlap
        // re-read (10 000 ids behind the cursor) still catches it.
        await InsertWithIdAsync(15_000, "late commit");
        await _detector.DetectOnceAsync(CancellationToken.None);
        Assert.Equal(1, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.episodes WHERE error_count = 2", 1));

        // Below the overlap window the poll no longer looks — the documented bound.
        await InsertWithIdAsync(5_000, "too old");
        await _detector.DetectOnceAsync(CancellationToken.None);
        // And a re-run changes nothing: the seen set keeps the overlap idempotent.
        await _detector.DetectOnceAsync(CancellationToken.None);

        Assert.Equal(1, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.episodes WHERE error_count = 2", 1));
        Assert.Equal(1, await _harness.WaitForCountAsync("SELECT COUNT(*) FROM alerting.episodes", 1));
    }

    [Fact]
    public async Task History_from_before_the_watch_began_never_alerts()
    {
        // Errors already in the store when alerting first runs are pre-install history: the
        // seeding run must not let the overlap re-read judge them afterwards. The scheduler's
        // startup tick has already seeded on an empty store, so simulate the install by
        // resetting the alerting state after the history exists.
        await _harness.ExecuteSqlAsync($$"""
            INSERT INTO telemetry.log_records
                (service_id, timestamp, severity_number, body, resource_attributes, attributes)
            VALUES ('{{_harness.ServiceId}}', now(), 21, 'ancient trouble', '{}', '{}');
            DELETE FROM alerting.episodes;
            DELETE FROM alerting.seen_log_ids;
            DELETE FROM alerting.poll_cursor;
            """);

        await _detector.DetectOnceAsync(CancellationToken.None); // Seeds at the existing log.
        await _detector.DetectOnceAsync(CancellationToken.None);

        Assert.Equal(0, await _harness.WaitForCountAsync("SELECT COUNT(*) FROM alerting.episodes", 0));

        // The watch is live from the seed forward: the next error still opens an episode.
        await _harness.ExecuteSqlAsync($$"""
            INSERT INTO telemetry.log_records
                (service_id, timestamp, severity_number, body, resource_attributes, attributes)
            VALUES ('{{_harness.ServiceId}}', now(), 17, 'fresh trouble', '{}', '{}')
            """);
        await _detector.DetectOnceAsync(CancellationToken.None);

        Assert.Equal(1, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.episodes WHERE first_log_body = 'fresh trouble'", 1));
    }

    [Fact]
    public async Task Sensitivity_off_turns_detection_into_a_cursor_advance()
    {
        await _harness.Client.PutAsJsonAsync(
            $"/api/admin/applications/{_harness.ApplicationId}/alerting",
            new { sensitivity = "Off", quietWindowMinutes = (int?)null });

        await _detector.DetectOnceAsync(CancellationToken.None); // Seeds the cursor.
        await _harness.ExecuteSqlAsync($$"""
            INSERT INTO telemetry.log_records
                (service_id, timestamp, severity_number, body, resource_attributes, attributes)
            VALUES ('{{_harness.ServiceId}}', now(), 21, 'unwatched', '{}', '{}')
            """);
        await _detector.DetectOnceAsync(CancellationToken.None);

        Assert.Equal(0, await _harness.WaitForCountAsync("SELECT COUNT(*) FROM alerting.episodes", 0));

        // Re-enabling does not replay history: the cursor moved past the unwatched log.
        await _harness.Client.PutAsJsonAsync(
            $"/api/admin/applications/{_harness.ApplicationId}/alerting",
            new { sensitivity = "Errors", quietWindowMinutes = (int?)null });
        await _detector.DetectOnceAsync(CancellationToken.None);

        Assert.Equal(0, await _harness.WaitForCountAsync("SELECT COUNT(*) FROM alerting.episodes", 0));
    }

    private Task InsertWithIdAsync(long id, string body) => _harness.ExecuteSqlAsync($$"""
        INSERT INTO telemetry.log_records
            (id, service_id, timestamp, severity_number, body, resource_attributes, attributes)
        OVERRIDING SYSTEM VALUE
        VALUES ({{id}}, '{{_harness.ServiceId}}', now(), 17, '{{body}}', '{}', '{}')
        """);
}
