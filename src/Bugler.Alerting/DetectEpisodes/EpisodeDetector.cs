using Bugler.Ai;
using Bugler.Alerting.Deliveries;
using Bugler.Alerting.Episodes;
using Bugler.Alerting.Readings;
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
    ISmtpSettingsSource smtpSettings,
    IAiSettingsSource aiSettings,
    ILogger<EpisodeDetector> logger)
{
    /// <summary>
    /// A page now carries stack traces rather than 500 bytes of body (ADR 0033), so it is a fifth
    /// of what it was: 1000 rows × 32 kB is the worst case, and the worst case is real.
    /// </summary>
    private const int PageSize = 1000;

    /// <summary>
    /// How much of a stack trace one row may carry. Past it the head and the tail are read and the
    /// middle is dropped: taking only the head would be wrong in both directions at once, because
    /// .NET puts the throw site first while Python puts the innermost frame last and Java appends
    /// the root cause at the end.
    /// </summary>
    private const int StackReadCap = 32 * 1024;

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
            // Detection never asks for a Quiet Window — but the snapshot is built whole, so
            // nobody can later read a half-resolved one.
            var fingerprintWindows = await dbContext.FingerprintQuietWindows.AsNoTracking()
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
                EffectiveSettings.Build(
                    catalog, applicationSettings, serviceSettings, fingerprintWindows),
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
        // Resolved once per run, exactly as the Sensitivity snapshot is.
        var attributeKeys = snapshot.Effective.NamedAttributeKeys();

        while (true)
        {
            var rows = await ReadMatchingPageAsync(readFrom, attributeKeys, cancellationToken);
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
        // This Watch's own Episodes only: another Watch's open Episode is neither one to feed
        // nor one whose kind of trouble means the same thing.
        var openEpisodes = await dbContext.Episodes
            .Where(e => e.ClosedAt == null && e.Watch == Watch.Logs)
            .ToDictionaryAsync(e => (e.ScopeKey, e.Fingerprint), cancellationToken);

        var decisions = DetectionBatch.Decide(
            rows, snapshot.Effective, openEpisodes.Keys.ToHashSet(), seenIds);

        // Every row read gets remembered, matched or not: what was observed under one
        // Sensitivity is never re-judged under a later one.
        var newlySeen = rows.Where(r => !seenIds.Contains(r.Id)).Select(r => r.Id).ToList();
        if (decisions.Scopes.Count == 0 && newlySeen.Count == 0)
        {
            return true;
        }

        var now = DateTimeOffset.UtcNow;
        var mailEnabled = (await smtpSettings.GetCurrentAsync(cancellationToken)).IsConfigured;
        var aiEnabled = (await aiSettings.GetCurrentAsync(cancellationToken)).IsConfigured;
        var aiConsent = scope.ServiceProvider.GetRequiredService<IAiConsentReader>();
        // One ask per Application per page; the writer asks again at the moment of disclosure.
        var consentByApplication = new Dictionary<ApplicationId, bool>();
        var storm = await StormWatch.OpenAsync(dbContext, decisions, options.Value, now, cancellationToken);
        var participations = await ParticipationsOfAsync(
            dbContext, decisions, openEpisodes, cancellationToken);

        foreach (var detection in decisions.Scopes)
        {
            var chatConfigured = snapshot.ApplicationsWithWebhook.Contains(detection.ApplicationId);
            if (detection.OpensWith is { } first)
            {
                var episode = new Episode
                {
                    Id = Guid.CreateVersion7(),
                    OpenedByServiceId = new ServiceId(first.ServiceId),
                    ApplicationId = detection.ApplicationId,
                    ScopeKey = detection.ScopeKey,
                    Watch = Watch.Logs,
                    Fingerprint = detection.Fingerprint,
                    Title = detection.Title,
                    RecipeVersion = Fingerprint.RecipeVersion,
                    FingerprintRung = detection.Rung,
                    StackTruncated = detection.StackTruncated,
                    OpenedAt = now,
                    FirstMatchLogId = first.Id,
                    FirstMatchAt = new DateTimeOffset(first.Timestamp, TimeSpan.Zero),
                    FirstMatchSeverity = first.Severity,
                    FirstMatchDetail = first.Body,
                    ErrorCount = detection.ErrorCount,
                    WarnCount = detection.WarnCount,
                    LastMatchAt = now,
                    AlertFoldedIntoStorm = storm.FoldsAlertsOf(detection.ScopeKey),
                };
                dbContext.Episodes.Add(episode);

                foreach (var tally in detection.Participants.Take(Participation.MaxPerEpisode))
                {
                    dbContext.Participations.Add(NewParticipation(episode.Id, tally, now));
                }

                if (episode.AlertFoldedIntoStorm)
                {
                    storm.Fold(episode);
                }
                else
                {
                    await AlertsOwed.EnqueueOpeningAsync(
                        dbContext, episode, mailEnabled, chatConfigured, now, cancellationToken);
                    // A page can open an Episode and drop other Services into it at once; their
                    // own followers are owed the joining message the opening one does not carry.
                    await OweJoiningAsync(
                        dbContext, episode, detection.Participants, episode.OpenedByServiceId,
                        mailEnabled, now, cancellationToken);
                }

                var consented = false;
                if (aiEnabled && !consentByApplication.TryGetValue(episode.ApplicationId, out consented))
                {
                    consented = await aiConsent.HasConsentAsync(episode.ApplicationId, cancellationToken);
                    consentByApplication[episode.ApplicationId] = consented;
                }

                // Owed only where both gates stand open now (ADR 0028): no pending Reading means
                // the Alert has nothing to hold the door for.
                if (aiEnabled && consented)
                {
                    dbContext.Readings.Add(new Reading
                    {
                        EpisodeId = episode.Id,
                        RequestedAt = now,
                        NextAttemptAt = now,
                    });
                }

                logger.LogInformation(
                    "Episode opened in scope {ScopeKey} by log {LogId} (severity {Severity}) "
                    + "on rung {Rung}: {Title}",
                    detection.ScopeKey, first.Id, first.Severity, detection.Rung, detection.Title);
            }
            else
            {
                var episode = openEpisodes[(detection.ScopeKey, detection.Fingerprint)];
                episode.ErrorCount += detection.ErrorCount;
                episode.WarnCount += detection.WarnCount;
                episode.LastMatchAt = now;

                var joined = FeedParticipations(
                    dbContext, episode, detection.Participants,
                    participations.GetValueOrDefault(episode.Id, []), now);
                await OweJoiningAsync(
                    dbContext, episode, joined, openedBy: null, mailEnabled, now, cancellationToken);
            }
        }

        await storm.OweDigestsAsync(
            dbContext, mailEnabled, snapshot.ApplicationsWithWebhook, now, cancellationToken);
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

    /// <summary>
    /// Feeds a running Episode's Participations and answers which tallies were new to it. A pair
    /// already there touches its last sighting and its counts; a pair past the ceiling counts on
    /// the Episode alone, because a sender putting a build id in `service.version` would
    /// otherwise open one Participation per process.
    /// </summary>
    private static List<ParticipantTally> FeedParticipations(
        AlertingDbContext dbContext,
        Episode episode,
        IReadOnlyList<ParticipantTally> tallies,
        List<Participation> existing,
        DateTimeOffset now)
    {
        var joined = new List<ParticipantTally>();
        foreach (var tally in tallies)
        {
            var held = existing.FirstOrDefault(
                p => p.ServiceId == tally.ServiceId && p.Version == tally.Version);
            if (held is not null)
            {
                held.LastAt = now;
                held.ErrorCount += tally.ErrorCount;
                held.WarnCount += tally.WarnCount;
                continue;
            }

            if (existing.Count >= Participation.MaxPerEpisode)
            {
                continue;
            }

            var opened = NewParticipation(episode.Id, tally, now);
            dbContext.Participations.Add(opened);
            existing.Add(opened);
            joined.Add(tally);
        }

        return joined;
    }

    private static Participation NewParticipation(
        Guid episodeId, ParticipantTally tally, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        EpisodeId = episodeId,
        ServiceId = tally.ServiceId,
        Version = tally.Version,
        FirstAt = now,
        LastAt = now,
        ErrorCount = tally.ErrorCount,
        WarnCount = tally.WarnCount,
    };

    /// <summary>Once per joining Service, not once per version: the message is about the Episode, not the build.</summary>
    private static async Task OweJoiningAsync(
        AlertingDbContext dbContext,
        Episode episode,
        IReadOnlyList<ParticipantTally> joined,
        ServiceId? openedBy,
        bool mailEnabled,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        foreach (var serviceId in joined.Select(t => t.ServiceId).Distinct()
                     .Where(id => id != openedBy))
        {
            await AlertsOwed.EnqueueJoiningAsync(
                dbContext, episode, serviceId, mailEnabled, now, cancellationToken);
        }
    }

    /// <summary>The Participations of every running Episode this page feeds — one query, not one per Episode.</summary>
    private static async Task<Dictionary<Guid, List<Participation>>> ParticipationsOfAsync(
        AlertingDbContext dbContext,
        DetectionDecisions decisions,
        IReadOnlyDictionary<(string, string), Episode> openEpisodes,
        CancellationToken cancellationToken)
    {
        var fed = decisions.Scopes
            .Where(d => d.OpensWith is null)
            .Select(d => openEpisodes[(d.ScopeKey, d.Fingerprint)].Id)
            .ToList();
        if (fed.Count == 0)
        {
            return [];
        }

        var rows = await dbContext.Participations
            .Where(p => fed.Contains(p.EpisodeId))
            .ToListAsync(cancellationToken);
        return rows.GroupBy(p => p.EpisodeId).ToDictionary(g => g.Key, g => g.ToList());
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
        long readFrom, IReadOnlyList<string> attributeKeys, CancellationToken cancellationToken)
    {
        // Everything a Fingerprint may be distilled from (ADR 0033): the sender's message template
        // — Serilog's `message_template.text` and MEL's `{OriginalFormat}` alike — the semantic
        // event name, the exception and its stack, and the Runtime that says how to read it. The
        // stack past the cap is read head and tail with a marker between; the severed lines at the
        // seam are dropped when it is parsed.
        await using var command = dataSource.CreateCommand(
            """
            SELECT id, service_id, timestamp, severity_number, left(body, 500),
                   COALESCE(attributes->>'message_template.text',
                            attributes->>'{OriginalFormat}'),
                   attributes->>'event.name',
                   attributes->>'exception.type',
                   CASE
                     WHEN attributes->>'exception.stacktrace' IS NULL THEN NULL
                     WHEN length(attributes->>'exception.stacktrace') <= @stackCap
                       THEN attributes->>'exception.stacktrace'
                     ELSE left(attributes->>'exception.stacktrace', @stackHalf) || @marker
                          || right(attributes->>'exception.stacktrace', @stackHalf)
                   END,
                   resource_attributes->>'telemetry.sdk.language',
                   resource_attributes->>'service.version',
                   (SELECT array_agg(attributes->>k ORDER BY ord)
                    FROM unnest(@attributeKeys) WITH ORDINALITY AS named(k, ord))
            FROM telemetry.log_records
            WHERE id > @readFrom AND severity_number >= @floor
            ORDER BY id
            LIMIT @limit
            """);
        command.Parameters.AddWithValue("readFrom", readFrom);
        command.Parameters.AddWithValue("floor", PolledSeverityFloor);
        command.Parameters.AddWithValue("limit", PageSize);
        command.Parameters.AddWithValue("stackCap", StackReadCap);
        command.Parameters.AddWithValue("stackHalf", StackReadCap / 2);
        command.Parameters.AddWithValue("marker", StackFrames.TruncationMarker);
        command.Parameters.Add(new NpgsqlParameter<string[]>(
            "attributeKeys", attributeKeys.ToArray()));

        var rows = new List<MatchingLog>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new MatchingLog(
                reader.GetInt64(0),
                reader.GetGuid(1),
                reader.GetDateTime(2),
                reader.GetInt16(3),
                await Text(reader, 4, cancellationToken),
                await Text(reader, 5, cancellationToken),
                await Text(reader, 6, cancellationToken),
                await Text(reader, 7, cancellationToken),
                await Text(reader, 8, cancellationToken),
                await Text(reader, 9, cancellationToken),
                await Text(reader, 10, cancellationToken),
                await NamedAttributesAsync(reader, attributeKeys, cancellationToken)));
        }

        return rows;
    }

    private static async Task<string?> Text(
        NpgsqlDataReader reader, int ordinal, CancellationToken cancellationToken) =>
        await reader.IsDBNullAsync(ordinal, cancellationToken) ? null : reader.GetString(ordinal);

    /// <summary>
    /// The named attributes come back as one array in the keys' own order — the alternative was a
    /// column per key, which the poll cannot shape because the keys are settings, not schema.
    /// </summary>
    private static async Task<IReadOnlyDictionary<string, string>?> NamedAttributesAsync(
        NpgsqlDataReader reader, IReadOnlyList<string> keys, CancellationToken cancellationToken)
    {
        if (keys.Count == 0 || await reader.IsDBNullAsync(11, cancellationToken))
        {
            return null;
        }

        var values = reader.GetFieldValue<string?[]>(11);
        Dictionary<string, string>? named = null;
        for (var i = 0; i < keys.Count && i < values.Length; i++)
        {
            if (values[i] is { } value)
            {
                named ??= [];
                named[keys[i]] = value;
            }
        }

        return named;
    }

    private sealed record Snapshot(
        EffectiveSettings Effective,
        HashSet<ApplicationId> ApplicationsWithWebhook,
        long CursorLastLogId,
        long WatchFloor);
}
