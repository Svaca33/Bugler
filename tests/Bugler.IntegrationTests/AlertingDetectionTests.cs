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
            builder.UseSetting("Mail:Smtp:Host", "smtp.test");
            builder.UseSetting("Mail:Smtp:From", "bugler@test.local");
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
            + "AND error_count = 1 AND warn_count = 0 AND first_match_detail = 'boom'", 1));
        Assert.Equal(1, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.deliveries WHERE kind = 1 AND channel = 1", 1));
        Assert.Equal(1, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.deliveries WHERE kind = 1 AND channel = 2", 1));

        // More of the same kind while open: counted, never re-announced. The warning stays
        // outside the default Errors sensitivity.
        await _harness.ExecuteSqlAsync($$"""
            INSERT INTO telemetry.log_records
                (service_id, timestamp, severity_number, body, resource_attributes, attributes)
            VALUES
                ('{{_harness.ServiceId}}', now(), 21, 'boom', '{}', '{}'),
                ('{{_harness.ServiceId}}', now(), 14, 'warned', '{}', '{}')
            """);
        await _detector.DetectOnceAsync(CancellationToken.None);

        Assert.Equal(1, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.episodes WHERE error_count = 2 AND warn_count = 0", 1));
        Assert.Equal(2, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.deliveries", 2));

        // A different kind of trouble is its own episode, announced on its own.
        await _harness.ExecuteSqlAsync($$"""
            INSERT INTO telemetry.log_records
                (service_id, timestamp, severity_number, body, resource_attributes, attributes)
            VALUES ('{{_harness.ServiceId}}', now(), 17, 'warehouse sync timed out', '{}', '{}')
            """);
        await _detector.DetectOnceAsync(CancellationToken.None);

        Assert.Equal(2, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.episodes WHERE closed_at IS NULL", 2));
        Assert.Equal(4, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.deliveries", 4));
    }

    [Fact]
    public async Task An_out_of_order_commit_inside_the_overlap_is_still_counted()
    {
        await _detector.DetectOnceAsync(CancellationToken.None); // Seeds the cursor at 0.

        await InsertWithIdAsync(20_000, "boom");
        await _detector.DetectOnceAsync(CancellationToken.None);
        Assert.Equal(1, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.episodes WHERE error_count = 1", 1));

        // A transaction holding a lower id committed after the poll passed it: the overlap
        // re-read (10 000 ids behind the cursor) still catches it.
        await InsertWithIdAsync(15_000, "boom");
        await _detector.DetectOnceAsync(CancellationToken.None);
        Assert.Equal(1, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.episodes WHERE error_count = 2", 1));

        // Below the overlap window the poll no longer looks — the documented bound.
        await InsertWithIdAsync(5_000, "boom");
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
            "SELECT COUNT(*) FROM alerting.episodes WHERE first_match_detail = 'fresh trouble'", 1));
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

    [Fact]
    public async Task Two_services_of_one_application_on_one_kind_of_trouble_meet_in_one_episode()
    {
        var (worker, _) = await _harness.SeedServiceAsync(
            _harness.ApplicationId, "acme", "prod", "worker");
        var (staging, _) = await _harness.SeedServiceAsync(
            _harness.ApplicationId, "acme", "staging", "web");

        await _detector.DetectOnceAsync(CancellationToken.None); // Seeds the cursor.
        await InsertThrownAsync(_harness.ServiceId, "1.4.0");
        await InsertThrownAsync(worker, "1.5.0");
        await InsertThrownAsync(staging, "1.5.0");
        await _detector.DetectOnceAsync(CancellationToken.None);

        // Environment stands by default, so production's two Services meet and staging does not.
        Assert.Equal(2, await _harness.WaitForCountAsync("SELECT COUNT(*) FROM alerting.episodes", 2));
        Assert.Equal(1, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.episodes WHERE error_count = 2", 1));

        // Two Participations, each with the version its own sender declared — the answer to
        // "is it still happening on the version we just shipped".
        Assert.Equal(2, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.participations p JOIN alerting.episodes e "
            + "ON e.id = p.episode_id WHERE e.error_count = 2", 2));
        Assert.Equal(1, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.participations WHERE version = '1.4.0'", 1));
    }

    [Fact]
    public async Task A_runtime_with_no_recipe_coarsens_visibly_instead_of_guessing()
    {
        await _detector.DetectOnceAsync(CancellationToken.None); // Seeds the cursor.

        // Two failures thrown in different places, in a Runtime Bugler has no recipe for: the
        // stack cannot be read, so both fall a rung to the kind of failure and meet there.
        await InsertRawAsync(
            "erlang", "badmatch",
            "** exception error: no match of right hand side value {error,timeout}\n"
            + "     in function  acme_pay:charge/2 (src/acme_pay.erl, line 42)");
        await InsertRawAsync(
            "erlang", "badmatch",
            "** exception error: no match of right hand side value {error,closed}\n"
            + "     in function  acme_ship:book/1 (src/acme_ship.erl, line 91)");
        await _detector.DetectOnceAsync(CancellationToken.None);

        // Rung 3 is the kind of failure — the degradation is on the Episode for anyone to see.
        Assert.Equal(1, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.episodes WHERE fingerprint_rung = 3 "
            + "AND error_count = 2 AND recipe_version = 1", 1));
    }

    [Fact]
    public async Task The_throwing_code_tells_two_call_sites_of_one_sentence_apart()
    {
        await _detector.DetectOnceAsync(CancellationToken.None); // Seeds the cursor.

        // The failure ADR 0033 exists to end: one generic template, two unrelated call sites.
        await InsertRawAsync(
            "dotnet", "MongoDB.Driver.MongoException",
            "System.Exception: boom\n   at Acme.Checkout.Commit(Order o) in /src/A.cs:line 3",
            template: "MongoDb transaction commit error");
        await InsertRawAsync(
            "dotnet", "MongoDB.Driver.MongoException",
            "System.Exception: boom\n   at Acme.Warehouse.Reserve(Order o) in /src/B.cs:line 7",
            template: "MongoDb transaction commit error");
        await _detector.DetectOnceAsync(CancellationToken.None);

        Assert.Equal(2, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.episodes WHERE fingerprint_rung = 2", 2));
        // The Title is what a person reads; the Fingerprint stands for the trouble.
        Assert.Equal(2, await _harness.WaitForCountAsync(
            "SELECT COUNT(*) FROM alerting.episodes "
            + "WHERE title = 'MongoException: MongoDb transaction commit error'", 2));
    }

    private Task InsertThrownAsync(Guid serviceId, string version) => _harness.ExecuteSqlAsync($$"""
        INSERT INTO telemetry.log_records
            (service_id, timestamp, severity_number, body, resource_attributes, attributes)
        VALUES ('{{serviceId}}', now(), 17, 'boom',
                '{"telemetry.sdk.language": "dotnet", "service.version": "{{version}}"}',
                '{"exception.type": "System.TimeoutException",
                  "exception.stacktrace": "System.TimeoutException: boom\n   at Acme.Pay.Charge(Order o) in /src/Pay.cs:line 42"}')
        """);

    private Task InsertRawAsync(
        string runtime, string exceptionType, string stack, string? template = null) =>
        _harness.ExecuteSqlAsync($$"""
        INSERT INTO telemetry.log_records
            (service_id, timestamp, severity_number, body, resource_attributes, attributes)
        VALUES ('{{_harness.ServiceId}}', now(), 17, 'boom',
                '{"telemetry.sdk.language": "{{runtime}}"}',
                jsonb_build_object(
                    'exception.type', '{{exceptionType}}',
                    'exception.stacktrace', {{Quote(stack)}}
                    {{(template is null ? "" : $", 'message_template.text', {Quote(template)}")}}))
        """);

    /// <summary>A PostgreSQL string literal that keeps the newlines a stack trace is made of.</summary>
    private static string Quote(string value) =>
        "E'" + value.Replace(@"\", @"\\").Replace("'", "''").Replace("\n", @"\n") + "'";

    private Task InsertWithIdAsync(long id, string body) => _harness.ExecuteSqlAsync($$"""
        INSERT INTO telemetry.log_records
            (id, service_id, timestamp, severity_number, body, resource_attributes, attributes)
        OVERRIDING SYSTEM VALUE
        VALUES ({{id}}, '{{_harness.ServiceId}}', now(), 17, '{{body}}', '{}', '{}')
        """);
}
