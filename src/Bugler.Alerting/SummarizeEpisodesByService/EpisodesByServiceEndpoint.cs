using System.Security.Claims;
using Bugler.Access.Contracts;
using Bugler.Alerting.Episodes;
using Bugler.Alerting.ListEpisodes;
using Bugler.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Alerting.SummarizeEpisodesByService;

/// <summary>
/// The one Episode a Service is quoted by: the newest open one when any is open, otherwise the
/// newest closed one — so a board can quote what burns or say when the last stretch ended.
/// </summary>
public sealed record EpisodeSummaryDto(
    Guid Id,
    EpisodeState State,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt,
    DateTimeOffset LastMatchAt,
    int ErrorCount,
    int WarnCount,
    Watch Watch,
    short? FirstMatchSeverity,
    string? FirstMatchDetail);

public sealed record ServiceEpisodesDto(
    Guid ServiceId,
    int Open,
    int Quieted,
    int Solved,
    int Muted,
    EpisodeSummaryDto? Latest);

/// <summary>A Service with no Episode under the filters is absent, not a zero row.</summary>
public sealed record EpisodesByServiceResponse(IReadOnlyList<ServiceEpisodesDto> Services);

internal static class EpisodesByServiceEndpoint
{
    /// <summary>
    /// The counts' breakdown turned sideways: one row per Service instead of one row of states,
    /// through the same filter chain, so a per-service board and the Episodes rail can never
    /// disagree about what the window holds.
    /// </summary>
    public static async Task<IResult> Handle(
        Guid? applicationId,
        Guid[]? serviceId,
        string? fingerprint,
        DateTimeOffset? from,
        string? q,
        string? acknowledged,
        ClaimsPrincipal principal,
        AlertingDbContext dbContext,
        IReadVisibility readVisibility,
        CancellationToken cancellationToken)
    {
        if (GetUserId(principal) is not { } callerId)
        {
            return Results.Unauthorized();
        }

        if (!EpisodeFilter.IsValidAcknowledged(acknowledged))
        {
            return Results.BadRequest("acknowledged must be \"none\" or \"me\".");
        }

        var visible = await readVisibility.GetVisibleApplicationsAsync(cancellationToken);
        if (visible is { Count: 0 })
        {
            return Results.Ok(new EpisodesByServiceResponse([]));
        }

        var filtered = dbContext.Episodes.AsNoTracking()
            .Apply(visible, applicationId, serviceId, fingerprint, from, q, acknowledged, callerId);

        // Same predicates as the state counts (ADR 0003 — the state is derived, never stored),
        // grouped by Service instead of folded into one row.
        var counts = await filtered
            .GroupBy(e => e.ServiceId)
            .Select(g => new
            {
                ServiceId = g.Key,
                Open = g.Count(e => e.ClosedAt == null),
                Quieted = g.Count(e =>
                    e.SolvedAt == null && e.CloseReason == EpisodeCloseReason.QuietWindow),
                Solved = g.Count(e => e.SolvedAt != null),
                Muted = g.Count(e =>
                    e.SolvedAt == null && e.CloseReason == EpisodeCloseReason.WatchOff),
            })
            .ToListAsync(cancellationToken);

        // Episode ids are UUIDv7, so the greatest id is the newest opening (same keyset the list
        // pages on). Both picks translate to one ROW_NUMBER pass per query.
        var latestOpen = (await filtered
                .Where(e => e.ClosedAt == null)
                .GroupBy(e => e.ServiceId)
                .Select(g => g.OrderByDescending(e => e.Id).First())
                .ToListAsync(cancellationToken))
            .ToDictionary(e => e.ServiceId);

        var latestClosed = (await filtered
                .Where(e => e.ClosedAt != null)
                .GroupBy(e => e.ServiceId)
                .Select(g => g.OrderByDescending(e => e.ClosedAt).ThenByDescending(e => e.Id).First())
                .ToListAsync(cancellationToken))
            .ToDictionary(e => e.ServiceId);

        var services = counts
            .OrderBy(c => c.ServiceId.Value)
            .Select(c => new ServiceEpisodesDto(
                c.ServiceId.Value, c.Open, c.Quieted, c.Solved, c.Muted,
                Summarize(
                    latestOpen.GetValueOrDefault(c.ServiceId)
                    ?? latestClosed.GetValueOrDefault(c.ServiceId))))
            .ToList();

        return Results.Ok(new EpisodesByServiceResponse(services));
    }

    private static EpisodeSummaryDto? Summarize(Episode? episode) =>
        episode is null
            ? null
            : new EpisodeSummaryDto(
                episode.Id, episode.State, episode.OpenedAt, episode.ClosedAt,
                episode.LastMatchAt, episode.ErrorCount, episode.WarnCount,
                episode.Watch, episode.FirstMatchSeverity, episode.FirstMatchDetail);

    // Access's claim helper is internal to Access; the two lines are cheaper than a contract.
    private static Guid? GetUserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
