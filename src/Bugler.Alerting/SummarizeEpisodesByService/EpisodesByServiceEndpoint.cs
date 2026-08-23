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
        string? scopeKey,
        string? fingerprint,
        DateTimeOffset? from,
        string? q,
        string? acknowledged,
        ClaimsPrincipal principal,
        AlertingDbContext dbContext,
        IReadVisibility readVisibility,
        IReadApplicationFocus readFocus,
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

        // The board is half the Dashboard, and the other half is Exploration's — both start from
        // the same set or the two would disagree about which Services exist (Access ADR 0004).
        var visible = await EpisodeFilter.ApplicationsForAsync(
            readVisibility, readFocus, applicationId, serviceId, cancellationToken);
        if (visible is { Count: 0 })
        {
            return Results.Ok(new EpisodesByServiceResponse([]));
        }

        var filtered = dbContext.Episodes.AsNoTracking()
            .Apply(dbContext, visible, applicationId, serviceId, scopeKey, fingerprint, from, q,
                acknowledged, callerId);

        // An Episode has no single Service (ADR 0034), so "this Service's Episodes" are the ones
        // it put something into — and one Episode fed by three Services is quoted by all three,
        // which is the honest answer to "what is burning here".
        var perService = filtered.SelectMany(
            e => dbContext.Participations.Where(p => p.EpisodeId == e.Id),
            (e, p) => new { p.ServiceId, Episode = e });

        // Same predicates as the state counts (ADR 0003 — the state is derived, never stored),
        // grouped by Service instead of folded into one row.
        var counts = await perService
            .GroupBy(row => row.ServiceId)
            .Select(g => new
            {
                ServiceId = g.Key,
                Open = g.Count(row => row.Episode.ClosedAt == null),
                Quieted = g.Count(row => row.Episode.SolvedAt == null
                    && row.Episode.CloseReason == EpisodeCloseReason.QuietWindow),
                Solved = g.Count(row => row.Episode.SolvedAt != null),
                Muted = g.Count(row => row.Episode.SolvedAt == null
                    && row.Episode.ClosedAt != null
                    && row.Episode.CloseReason != EpisodeCloseReason.QuietWindow),
            })
            .ToListAsync(cancellationToken);

        // Episode ids are UUIDv7, so the greatest id is the newest opening (same keyset the list
        // pages on). Both picks translate to one ROW_NUMBER pass per query.
        var latestOpen = (await perService
                .Where(row => row.Episode.ClosedAt == null)
                .GroupBy(row => row.ServiceId)
                .Select(g => g.OrderByDescending(row => row.Episode.Id).First())
                .ToListAsync(cancellationToken))
            .ToDictionary(row => row.ServiceId, row => row.Episode);

        var latestClosed = (await perService
                .Where(row => row.Episode.ClosedAt != null)
                .GroupBy(row => row.ServiceId)
                .Select(g => g
                    .OrderByDescending(row => row.Episode.ClosedAt)
                    .ThenByDescending(row => row.Episode.Id)
                    .First())
                .ToListAsync(cancellationToken))
            .ToDictionary(row => row.ServiceId, row => row.Episode);

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
