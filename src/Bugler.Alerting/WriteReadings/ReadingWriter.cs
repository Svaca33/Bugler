using Bugler.Ai;
using Bugler.Alerting.DeliverMessages;
using Bugler.Alerting.Episodes;
using Bugler.Alerting.Readings;
using Bugler.Registry.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Bugler.Alerting.WriteReadings;

/// <summary>
/// One writing sweep: pursue the Readings still owed, oldest first. Consent and the AI settings
/// are read now, at the moment the evidence would leave — never earlier (ADR 0028) — and either
/// gone means the row ends as Failed rather than waiting on what will not come: a terminal state
/// is what releases the Alert holding the door (Alerting ADR 0009). The evidence is read from
/// the telemetry schema on ADR 0010's terms: SQL, never Ingestion's assembly, never a write.
/// </summary>
public sealed class ReadingWriter(
    IServiceScopeFactory scopeFactory,
    NpgsqlDataSource dataSource,
    IAiSettingsSource aiSettings,
    IAiCompletion completion,
    ILogger<ReadingWriter> logger)
{
    private const int BatchSize = 10;
    private const int PriorLogCount = 25;
    private const int PriorLogBodyChars = 400;
    private const int OpeningAttributesChars = 4000;

    public async Task WriteOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AlertingDbContext>();
        var now = DateTimeOffset.UtcNow;

        var due = await dbContext.Readings
            .Where(r => r.WrittenAt == null && r.FailedAt == null && r.NextAttemptAt <= now)
            .OrderBy(r => r.RequestedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);
        if (due.Count == 0)
        {
            return;
        }

        var episodeIds = due.Select(r => r.EpisodeId).ToList();
        var episodes = await dbContext.Episodes
            .AsNoTracking()
            .Where(e => episodeIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, cancellationToken);

        var catalog = await scope.ServiceProvider.GetRequiredService<ICatalogReader>()
            .GetServicesAsync(cancellationToken);
        var identityByService = catalog.ToDictionary(s => s.Id);
        var consentReader = scope.ServiceProvider.GetRequiredService<IAiConsentReader>();

        foreach (var reading in due)
        {
            if (!episodes.TryGetValue(reading.EpisodeId, out var episode)
                || episode.OpenedByServiceId is not { } opener
                || !identityByService.TryGetValue(opener, out var identity))
            {
                // The Service is gone; the cascade will collect this row shortly.
                Fail(reading, "The service is no longer registered.");
                await dbContext.SaveChangesAsync(cancellationToken);
                continue;
            }

            var settings = await aiSettings.GetCurrentAsync(cancellationToken);
            if (!settings.IsConfigured)
            {
                Fail(reading, "AI is no longer configured on this server.");
            }
            else if (!await consentReader.HasConsentAsync(episode.ApplicationId, cancellationToken))
            {
                // Withdrawn between the opening and now; withdrawing stops the very next
                // disclosure, so nothing is gathered and nothing leaves (ADR 0028).
                Fail(reading, "The application's AI consent is withdrawn.");
            }
            else
            {
                await AttemptAsync(reading, episode, identity, settings, cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task AttemptAsync(
        Reading reading,
        Episode episode,
        CatalogService identity,
        AiSettings settings,
        CancellationToken cancellationToken)
    {
        try
        {
            var evidence = await GatherEvidenceAsync(episode, cancellationToken);
            var answer = await completion.CompleteAsync(
                ReadingPrompt.Compose(episode, identity, evidence), cancellationToken);
            var (english, czech) = ReadingPrompt.ParseAnswer(answer);

            reading.English = english;
            reading.Czech = czech;
            reading.Model = settings.Model;
            reading.WrittenAt = DateTimeOffset.UtcNow;
            logger.LogInformation(
                "Reading written for episode {EpisodeId} on attempt {Attempt}",
                reading.EpisodeId, reading.Attempts + 1);
        }
        catch (AiException exception)
        {
            reading.Attempts += 1;
            reading.LastError = Truncate(exception.Message);
            if (reading.Attempts >= Reading.MaxAttempts)
            {
                // Terminal, so the Alert stops holding the door even on "as long as it takes".
                reading.FailedAt = DateTimeOffset.UtcNow;
                logger.LogWarning(
                    "Reading for episode {EpisodeId} failed for good after {Attempts} attempts: {Error}",
                    reading.EpisodeId, reading.Attempts, exception.Message);
            }
            else
            {
                reading.NextAttemptAt = DateTimeOffset.UtcNow + RetrySchedule.NextDelay(reading.Attempts);
                logger.LogWarning(
                    "Reading for episode {EpisodeId} failed on attempt {Attempts}: {Error}",
                    reading.EpisodeId, reading.Attempts, exception.Message);
            }
        }
    }

    private async Task<ReadingEvidence> GatherEvidenceAsync(
        Episode episode, CancellationToken cancellationToken)
    {
        // Purged or never there, the Reading is written from what remains — the Episode's own
        // snapshot always survives.
        var attributes = episode.FirstMatchLogId is { } logId
            ? await ReadOpeningAttributesAsync(logId, cancellationToken)
            : null;
        var priorLogs = await ReadPriorLogsAsync(episode, cancellationToken);
        var lastRelease = await ReadLastReleaseAsync(episode, cancellationToken);
        return new ReadingEvidence(attributes, priorLogs, lastRelease);
    }

    private async Task<string?> ReadOpeningAttributesAsync(long logId, CancellationToken cancellationToken)
    {
        // The attributes ride along for the opening match alone: they carry the stack trace, and
        // the stack trace is the triage (ADR 0028 names them in the consent's wording).
        await using var command = dataSource.CreateCommand(
            "SELECT left(attributes::text, @limit) FROM telemetry.log_records WHERE id = @id");
        command.Parameters.AddWithValue("id", logId);
        command.Parameters.AddWithValue("limit", OpeningAttributesChars);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is string { Length: > 2 } text ? text : null; // "{}" says nothing.
    }

    private async Task<IReadOnlyList<EvidenceLog>> ReadPriorLogsAsync(
        Episode episode, CancellationToken cancellationToken)
    {
        // Under the Logs Watch the opening log's id bounds "before" exactly; the Health Check
        // Watch has no log to point at, so the opening moment bounds it instead.
        await using var command = episode.FirstMatchLogId is { } logId
            ? dataSource.CreateCommand(
                """
                SELECT timestamp, severity_number, left(body, @chars)
                FROM telemetry.log_records
                WHERE service_id = @serviceId AND id < @beforeId
                ORDER BY id DESC
                LIMIT @limit
                """)
            : dataSource.CreateCommand(
                """
                SELECT timestamp, severity_number, left(body, @chars)
                FROM telemetry.log_records
                WHERE service_id = @serviceId AND timestamp <= @before
                ORDER BY timestamp DESC
                LIMIT @limit
                """);
        command.Parameters.AddWithValue("serviceId", episode.OpenedByServiceId!.Value.Value);
        if (episode.FirstMatchLogId is { } id)
        {
            command.Parameters.AddWithValue("beforeId", id);
        }
        else
        {
            command.Parameters.AddWithValue("before", episode.OpenedAt.UtcDateTime);
        }

        command.Parameters.AddWithValue("chars", PriorLogBodyChars);
        command.Parameters.AddWithValue("limit", PriorLogCount);

        var logs = new List<EvidenceLog>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            logs.Add(new EvidenceLog(
                reader.GetDateTime(0),
                reader.GetInt16(1),
                await reader.IsDBNullAsync(2, cancellationToken) ? null : reader.GetString(2)));
        }

        return logs;
    }

    private async Task<ReleaseFact?> ReadLastReleaseAsync(
        Episode episode, CancellationToken cancellationToken)
    {
        await using var command = dataSource.CreateCommand(
            """
            SELECT version, previous_version, observed_at
            FROM telemetry.releases
            WHERE service_id = @serviceId AND observed_at <= @before
            ORDER BY observed_at DESC, id DESC
            LIMIT 1
            """);
        command.Parameters.AddWithValue("serviceId", episode.OpenedByServiceId!.Value.Value);
        command.Parameters.AddWithValue("before", episode.OpenedAt.UtcDateTime);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new ReleaseFact(
            reader.GetString(0),
            await reader.IsDBNullAsync(1, cancellationToken) ? null : reader.GetString(1),
            reader.GetDateTime(2));
    }

    private static void Fail(Reading reading, string reason)
    {
        reading.FailedAt = DateTimeOffset.UtcNow;
        reading.LastError = reason;
    }

    private static string Truncate(string error) =>
        error.Length <= 2000 ? error : error[..2000];
}
