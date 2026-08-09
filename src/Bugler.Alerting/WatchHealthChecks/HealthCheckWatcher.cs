using System.Collections.Concurrent;
using Bugler.Ai;
using Bugler.Alerting.Deliveries;
using Bugler.Alerting.DetectEpisodes;
using Bugler.Alerting.Episodes;
using Bugler.Alerting.Readings;
using Bugler.Alerting.Settings;
using Bugler.Mail;
using Bugler.Registry.Contracts;
using Bugler.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Bugler.Alerting.WatchHealthChecks;

/// <summary>
/// One sweep of the Health Check Watch: asks every Service that has an address whether it is
/// alive, and turns the answers into Episodes. The only place in Bugler that opens a connection
/// outwards.
///
/// The tally of consecutive failures lives in memory and nowhere else. Losing it to a restart
/// costs a few seconds of detection and nothing more — the Episode, which is the durable thing,
/// is already in the database — and persisting it would mean a write every fifteen seconds per
/// Service to record that everything is fine.
/// </summary>
public sealed class HealthCheckWatcher(
    IServiceScopeFactory scopeFactory,
    ISmtpSettingsSource smtpSettings,
    IAiSettingsSource aiSettings,
    ILogger<HealthCheckWatcher> logger)
{
    /// <summary>Enough to keep one unresponsive Service from holding up the loop; low enough not to look like a flood.</summary>
    private const int MaxConcurrentProbes = 8;

    private readonly ConcurrentDictionary<ServiceId, int> _consecutiveFailures = new();

    public async Task WatchOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AlertingDbContext>();
        var catalog = await scope.ServiceProvider.GetRequiredService<ICatalogReader>()
            .GetServicesAsync(cancellationToken);
        var applicationSettings = await dbContext.ApplicationSettings.AsNoTracking()
            .ToListAsync(cancellationToken);
        var effective = EffectiveSettings.Build(
            catalog,
            applicationSettings,
            await dbContext.ServiceSettings.AsNoTracking().ToListAsync(cancellationToken),
            await dbContext.FingerprintQuietWindows.AsNoTracking().ToListAsync(cancellationToken));

        var watched = effective.WatchedByHealthCheck();
        Forget(watched);
        if (watched.Count == 0)
        {
            return;
        }

        var probe = scope.ServiceProvider.GetRequiredService<HealthProbe>();
        var outcomes = new ConcurrentDictionary<ServiceId, ProbeOutcome>();
        await Parallel.ForEachAsync(
            watched,
            new ParallelOptions
            {
                MaxDegreeOfParallelism = MaxConcurrentProbes,
                CancellationToken = cancellationToken,
            },
            async (service, token) =>
                outcomes[service.ServiceId] = await probe.ProbeAsync(service.Url, token));

        await ApplyAsync(
            dbContext, watched, outcomes, applicationSettings,
            scope.ServiceProvider.GetRequiredService<IAiConsentReader>(), cancellationToken);
    }

    /// <summary>Applies one sweep's verdicts in one transaction, so an Episode never exists without its Alerts.</summary>
    private async Task ApplyAsync(
        AlertingDbContext dbContext,
        IReadOnlyList<WatchedService> watched,
        IReadOnlyDictionary<ServiceId, ProbeOutcome> outcomes,
        IReadOnlyList<ApplicationAlertingSettings> applicationSettings,
        IAiConsentReader aiConsent,
        CancellationToken cancellationToken)
    {
        // At most one per Service: the reserved Fingerprint makes the one-open-per-kind index
        // say "one open health check Episode per Service" without a second index.
        var openEpisodes = await dbContext.Episodes
            .Where(e => e.ClosedAt == null && e.Watch == Watch.HealthCheck)
            .ToDictionaryAsync(e => e.ServiceId, cancellationToken);
        var chatApplications = applicationSettings
            .Where(s => s.ChatWebhookUrl is not null)
            .Select(s => s.ApplicationId)
            .ToHashSet();
        var mailEnabled = (await smtpSettings.GetCurrentAsync(cancellationToken)).IsConfigured;
        var aiEnabled = (await aiSettings.GetCurrentAsync(cancellationToken)).IsConfigured;

        var now = DateTimeOffset.UtcNow;
        var changed = false;

        foreach (var service in watched)
        {
            if (!outcomes.TryGetValue(service.ServiceId, out var outcome))
            {
                continue;
            }

            var failures = Tally(service.ServiceId, outcome.Alive);
            var open = openEpisodes.GetValueOrDefault(service.ServiceId);

            switch (HealthWatchDecision.Decide(outcome.Alive, failures, open is not null))
            {
                case HealthAction.Open:
                    var episode = new Episode
                    {
                        Id = Guid.CreateVersion7(),
                        ServiceId = service.ServiceId,
                        ApplicationId = service.ApplicationId,
                        Watch = Watch.HealthCheck,
                        Fingerprint = Fingerprint.HealthCheckFailing,
                        OpenedAt = now,
                        // No Log Record to point at and no Severity Band to speak of: this Watch
                        // deals in neither. What it saw is the detail.
                        FirstMatchAt = now,
                        FirstMatchDetail = Truncate(outcome.Detail),
                        LastMatchAt = now,
                    };
                    dbContext.Episodes.Add(episode);
                    await AlertsOwed.EnqueueAsync(
                        dbContext, episode, mailEnabled,
                        chatApplications.Contains(service.ApplicationId), now, cancellationToken);
                    // Owed only where both gates stand open now (ADR 0028); generation asks again.
                    if (aiEnabled && await aiConsent.HasConsentAsync(episode.ApplicationId, cancellationToken))
                    {
                        dbContext.Readings.Add(new Reading
                        {
                            EpisodeId = episode.Id,
                            RequestedAt = now,
                            NextAttemptAt = now,
                        });
                    }
                    logger.LogWarning(
                        "Service {ServiceId} stopped answering its health check: {Detail}",
                        service.ServiceId, outcome.Detail);
                    changed = true;
                    break;

                case HealthAction.Feed:
                    open!.LastMatchAt = now;
                    changed = true;
                    break;
            }
        }

        if (!changed)
        {
            return;
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            // Two sweeps racing (tests; never the single scheduler) — the one-open-episode index
            // won. Nothing committed; the next sweep sees the Episode and feeds it instead.
            logger.LogWarning(exception, "Health check sweep conflicted and will be retried");
        }
    }

    /// <summary>A success wipes the tally; a failure adds to it. Only the run of failures matters.</summary>
    private int Tally(ServiceId serviceId, bool alive)
    {
        if (!alive)
        {
            return _consecutiveFailures.AddOrUpdate(serviceId, 1, (_, failures) => failures + 1);
        }

        _consecutiveFailures[serviceId] = 0;
        return 0;
    }

    /// <summary>A Service nobody asks any more drops its tally, so a later address starts from nothing.</summary>
    private void Forget(IReadOnlyList<WatchedService> watched)
    {
        var stillWatched = watched.Select(w => w.ServiceId).ToHashSet();
        foreach (var serviceId in _consecutiveFailures.Keys.Where(id => !stillWatched.Contains(id)))
        {
            _consecutiveFailures.TryRemove(serviceId, out _);
        }
    }

    /// <summary>The Episode's detail column is bounded; a long address must not cost the reason.</summary>
    private static string Truncate(string detail) =>
        detail.Length <= 500 ? detail : detail[..500];
}
