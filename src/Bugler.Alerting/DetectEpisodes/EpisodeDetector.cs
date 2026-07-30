using Bugler.Alerting.Deliveries;
using Bugler.Alerting.Episodes;
using Bugler.Alerting.Settings;
using Bugler.Mail;
using Bugler.Registry.Contracts;
using Bugler.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Bugler.Alerting.DetectEpisodes;

/// <summary>
/// One detection sweep over `telemetry.log_records` (ADR 0010): reads matching Log Records past
/// the cursor, opens or feeds Episodes, and writes the owed Alert Deliveries — all of one page
/// in one transaction, so an Episode can never exist without its Alerts nor the other way round.
/// </summary>
public sealed class EpisodeDetector(
    IServiceScopeFactory scopeFactory,
    NpgsqlDataSource dataSource,
    IOptions<AlertingOptions> options,
    IOptions<MailOptions> mailOptions,
    ILogger<EpisodeDetector> logger)
{
    private const int PageSize = 5000;

    /// <summary>
    /// The poll always reads at the lowest floor any Sensitivity can want (Warn, 13) and records
    /// everything it read — so a Log Record is judged exactly once, under the Sensitivity in
    /// force when it was first observed. Raising Sensitivity later never re-judges old rows: an
    /// alert about trouble from before the watching started is the stale panic this feature
    /// exists to avoid. Matches the partial index `ix_log_records_alerting_poll`.
    /// </summary>
    private const short PolledSeverityFloor = 13;

    public async Task DetectOnceAsync(CancellationToken cancellationToken)
    {
        // The high-water mark is taken BEFORE the match read: a row committing after this point
        // is either above it or inside the next run's overlap — never silently skipped.
        var newHighWaterMark = await ReadHighWaterMarkAsync(cancellationToken);

        Snapshot snapshot;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var catalog = await scope.ServiceProvider.GetRequiredService<ICatalogReader>()
                .GetServicesAsync(cancellationToken);
            var dbContext = scope.ServiceProvider.GetRequiredService<AlertingDbContext>();
            var applicationSettings = await dbContext.ApplicationSettings.AsNoTracking()
                .ToListAsync(cancellationToken);
            var serviceSettings = await dbContext.ServiceSettings.AsNoTracking()
                .ToListAsync(cancellationToken);
            var cursor = await dbContext.PollCursor.AsNoTracking()
                .FirstOrDefaultAsync(cancellationToken);

            if (cursor is null)
            {
                // First run ever: watch from install time forward. Replaying history here would
                // flood alerts about long-dead trouble.
                dbContext.PollCursor.Add(new PollCursor
                {
                    Id = PollCursor.SingletonId,
                    LastLogId = newHighWaterMark,
                    WatchFloor = newHighWaterMark,
                    UpdatedAt = DateTimeOffset.UtcNow,
                });
                await dbContext.SaveChangesAsync(cancellationToken);
                logger.LogInformation(
                    "Alerting cursor seeded at log id {HighWaterMark}; watching from here on",
                    newHighWaterMark);
                return;
            }

            snapshot = new Snapshot(
                EffectiveSettings.Build(catalog, applicationSettings, serviceSettings),
                applicationSettings
                    .Where(s => s.ChatWebhookUrl is not null)
                    .Select(s => s.ApplicationId)
                    .ToHashSet(),
                cursor.LastLogId,
                cursor.WatchFloor);
        }

        var overlap = options.Value.DetectionOverlapIds;
        // The overlap never reaches under the watch floor: what predates the watch is history.
        var readFrom = Math.Max(
            Math.Max(0, snapshot.CursorLastLogId - overlap), snapshot.WatchFloor);
        var highestProcessed = snapshot.CursorLastLogId;

        while (true)
        {
            var rows = await ReadMatchingPageAsync(readFrom, cancellationToken);
            if (rows.Count == 0)
            {
                break;
            }

            if (!await ProcessPageAsync(rows, snapshot, cancellationToken))
            {
                return; // Conflict logged; the unmoved cursor makes the next run retry.
            }

            readFrom = rows[^1].Id;
            highestProcessed = Math.Max(highestProcessed, readFrom);
            if (rows.Count < PageSize)
            {
                break;
            }
        }

        await FinishAsync(Math.Max(highestProcessed, newHighWaterMark), cancellationToken);
    }

    /// <summary>Applies one page of decisions in one transaction. False means a conflict rolled it back.</summary>
    private async Task<bool> ProcessPageAsync(
        IReadOnlyList<MatchingLog> rows, Snapshot snapshot, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AlertingDbContext>();

        var seenIds = (await dbContext.SeenLogIds.AsNoTracking()
                .Select(s => s.LogId)
                .ToListAsync(cancellationToken))
            .ToHashSet();
        var openEpisodes = await dbContext.Episodes
            .Where(e => e.ClosedAt == null)
            .ToDictionaryAsync(e => (e.ServiceId, e.Fingerprint), cancellationToken);

        var decisions = DetectionBatch.Decide(
            rows, snapshot.Effective, openEpisodes.Keys.ToHashSet(), seenIds);

        // Every row read gets remembered, matched or not: what was observed under one
        // Sensitivity is never re-judged under a later one.
        var newlySeen = rows.Where(r => !seenIds.Contains(r.Id)).Select(r => r.Id).ToList();
        if (decisions.Services.Count == 0 && newlySeen.Count == 0)
        {
            return true;
        }

        var now = DateTimeOffset.UtcNow;
        var mailEnabled = mailOptions.Value.Smtp.IsConfigured;

        foreach (var detection in decisions.Services)
        {
            if (detection.OpensWith is { } first)
            {
                var episode = new Episode
                {
                    Id = Guid.CreateVersion7(),
                    ServiceId = detection.ServiceId,
                    ApplicationId = detection.ApplicationId,
                    Fingerprint = detection.Fingerprint,
                    OpenedAt = now,
                    FirstLogId = first.Id,
                    FirstLogTimestamp = new DateTimeOffset(first.Timestamp, TimeSpan.Zero),
                    FirstLogSeverity = first.Severity,
                    FirstLogBody = first.Body,
                    ErrorCount = detection.ErrorCount,
                    WarnCount = detection.WarnCount,
                    LastMatchAt = now,
                };
                dbContext.Episodes.Add(episode);
                await EnqueueAlertsAsync(dbContext, episode, snapshot, mailEnabled, now, cancellationToken);
                logger.LogInformation(
                    "Episode opened for service {ServiceId} by log {LogId} (severity {Severity}): {Fingerprint}",
                    detection.ServiceId, first.Id, first.Severity, detection.Fingerprint);
            }
            else
            {
                var episode = openEpisodes[(detection.ServiceId, detection.Fingerprint)];
                episode.ErrorCount += detection.ErrorCount;
                episode.WarnCount += detection.WarnCount;
                episode.LastMatchAt = now;
            }
        }

        dbContext.SeenLogIds.AddRange(newlySeen.Select(id => new SeenLogId { LogId = id }));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception)
        {
            // Two detectors racing (tests; never the single scheduler) — the one-open-episode
            // index won. Nothing committed, nothing advanced; the next run reconciles.
            logger.LogWarning(exception, "Detection page conflicted and will be retried");
            return false;
        }
    }

    /// <summary>The Alert rows owed for a fresh Episode: one mail per subscriber now, one chat message if the webhook is set.</summary>
    private static async Task EnqueueAlertsAsync(
        AlertingDbContext dbContext,
        Episode episode,
        Snapshot snapshot,
        bool mailEnabled,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (mailEnabled)
        {
            // No access check here: whether a subscriber may still read the Application is a
            // delivery-time question. Subscribers are resolved at open — the Alert's audience.
            var subscribers = await dbContext.Subscriptions
                .Where(s => s.ServiceId == episode.ServiceId || s.ApplicationId == episode.ApplicationId)
                .Select(s => s.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);
            foreach (var userId in subscribers)
            {
                dbContext.Deliveries.Add(new Delivery
                {
                    Id = Guid.CreateVersion7(),
                    EpisodeId = episode.Id,
                    Kind = DeliveryKind.Alert,
                    Channel = DeliveryChannel.Mail,
                    UserId = userId,
                    CreatedAt = now,
                    NextAttemptAt = now,
                });
            }
        }

        if (snapshot.ApplicationsWithWebhook.Contains(episode.ApplicationId))
        {
            dbContext.Deliveries.Add(new Delivery
            {
                Id = Guid.CreateVersion7(),
                EpisodeId = episode.Id,
                Kind = DeliveryKind.Alert,
                Channel = DeliveryChannel.Chat,
                CreatedAt = now,
                NextAttemptAt = now,
            });
        }
    }

    /// <summary>Advances the cursor (never backwards) and prunes seen ids the overlap can no longer reach.</summary>
    private async Task FinishAsync(long cursorTarget, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AlertingDbContext>();

        var cursor = await dbContext.PollCursor.FirstAsync(cancellationToken);
        if (cursorTarget > cursor.LastLogId)
        {
            cursor.LastLogId = cursorTarget;
        }

        cursor.UpdatedAt = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        var reachable = cursor.LastLogId - options.Value.DetectionOverlapIds;
        await dbContext.SeenLogIds
            .Where(s => s.LogId <= reachable)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private async Task<long> ReadHighWaterMarkAsync(CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            "SELECT COALESCE(MAX(id), 0) FROM telemetry.log_records");
        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    private async Task<IReadOnlyList<MatchingLog>> ReadMatchingPageAsync(
        long readFrom, CancellationToken cancellationToken)
    {
        // The two attributes a Fingerprint prefers over the body: the sender's message template
        // (`{OriginalFormat}`, as .NET loggers ship it) and the semantic event name.
        await using var command = dataSource.CreateCommand(
            """
            SELECT id, service_id, timestamp, severity_number, left(body, 500),
                   attributes->>'{OriginalFormat}', attributes->>'event.name'
            FROM telemetry.log_records
            WHERE id > @readFrom AND severity_number >= @floor
            ORDER BY id
            LIMIT @limit
            """);
        command.Parameters.AddWithValue("readFrom", readFrom);
        command.Parameters.AddWithValue("floor", PolledSeverityFloor);
        command.Parameters.AddWithValue("limit", PageSize);

        var rows = new List<MatchingLog>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new MatchingLog(
                reader.GetInt64(0),
                reader.GetGuid(1),
                reader.GetDateTime(2),
                reader.GetInt16(3),
                await reader.IsDBNullAsync(4, cancellationToken) ? null : reader.GetString(4),
                await reader.IsDBNullAsync(5, cancellationToken) ? null : reader.GetString(5),
                await reader.IsDBNullAsync(6, cancellationToken) ? null : reader.GetString(6)));
        }

        return rows;
    }

    private sealed record Snapshot(
        EffectiveSettings Effective,
        HashSet<ApplicationId> ApplicationsWithWebhook,
        long CursorLastLogId,
        long WatchFloor);
}
