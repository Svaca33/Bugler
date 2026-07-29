using Bugler.Access.Contracts;
using Bugler.Alerting.Episodes;
using Bugler.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Bugler.Alerting.ListEpisodes;

public sealed record EpisodeDto(
    Guid Id,
    Guid ApplicationId,
    Guid ServiceId,
    DateTimeOffset OpenedAt,
    DateTimeOffset? ClosedAt,
    EpisodeCloseReason? CloseReason,
    DateTimeOffset LastMatchAt,
    int ErrorCount,
    int WarnCount,
    long FirstLogId,
    DateTimeOffset FirstLogTimestamp,
    short FirstLogSeverity,
    string? FirstLogBody);

public sealed record ListEpisodesResponse(IReadOnlyList<EpisodeDto> Items);

internal static class EpisodesEndpoint
{
    /// <summary>
    /// Episodes within the caller's Visibility Scope, newest first. Scope needs no catalog
    /// round-trip here: an Episode carries its ApplicationId and grants are per Application.
    /// </summary>
    public static async Task<IResult> Handle(
        Guid? applicationId,
        Guid? serviceId,
        bool? open,
        Guid? beforeId,
        int? limit,
        AlertingDbContext dbContext,
        IReadVisibility readVisibility,
        CancellationToken cancellationToken)
    {
        var visible = await readVisibility.GetVisibleApplicationsAsync(cancellationToken);
        if (visible is { Count: 0 })
        {
            return Results.Ok(new ListEpisodesResponse([]));
        }

        var query = dbContext.Episodes.AsNoTracking().AsQueryable();
        if (visible is not null)
        {
            var visibleIds = visible.ToList();
            query = query.Where(e => visibleIds.Contains(e.ApplicationId));
        }

        if (applicationId is { } application)
        {
            var id = new ApplicationId(application);
            query = query.Where(e => e.ApplicationId == id);
        }

        if (serviceId is { } service)
        {
            var id = new ServiceId(service);
            query = query.Where(e => e.ServiceId == id);
        }

        if (open == true)
        {
            query = query.Where(e => e.ClosedAt == null);
        }

        // Episode ids are UUIDv7 and PostgreSQL compares uuids bytewise, so id order is open
        // order — one keyset column covers the (opened_at, id) ordering.
        if (beforeId is { } before)
        {
            query = query.Where(e => e.Id.CompareTo(before) < 0);
        }

        var take = Math.Clamp(limit ?? 100, 1, 500);
        var items = await query
            .OrderByDescending(e => e.Id)
            .Take(take)
            .Select(e => new EpisodeDto(
                e.Id, e.ApplicationId.Value, e.ServiceId.Value, e.OpenedAt, e.ClosedAt,
                e.CloseReason, e.LastMatchAt, e.ErrorCount, e.WarnCount,
                e.FirstLogId, e.FirstLogTimestamp, e.FirstLogSeverity, e.FirstLogBody))
            .ToListAsync(cancellationToken);

        return Results.Ok(new ListEpisodesResponse(items));
    }
}
