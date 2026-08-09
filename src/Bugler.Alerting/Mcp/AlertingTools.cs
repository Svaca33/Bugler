using System.ComponentModel;
using Bugler.Access.Contracts;
using Bugler.Alerting.Episodes;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Bugler.Alerting.Mcp;

/// <summary>
/// What Alerting answers at the machine door: the Episodes, and nothing that changes one. A
/// Machine Delegation cannot write at all (ADR 0029), so Acknowledge, Solve and the Quiet Window are absent
/// by construction rather than by restraint — Solved is a human verdict and stays one.
///
/// These are the entry point of a debugging session: an Episode is where Bugler says of its own
/// accord that something is wrong, and it costs a fraction of what the same conclusion costs when
/// reached by reading log records.
/// </summary>
[McpServerToolType]
public sealed class AlertingTools
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 200;

    [McpServerTool(Name = "list_episodes", ReadOnly = true)]
    [Description(
        "The Episodes in view: one per kind of trouble in one Service, opened when a Service " +
        "started logging that trouble or stopped answering that it is alive, and closed by a " +
        "Quiet Window of silence. Start here — this is what Bugler noticed without being asked. " +
        "States: Open (still happening), Quieted (went quiet on its own), Solved (a person said " +
        "so), Muted. Defaults to the Episodes that are still open.")]
    public static async Task<EpisodeListAnswer> ListEpisodesAsync(
        AlertingDbContext dbContext,
        IReadVisibility readVisibility,
        CancellationToken cancellationToken,
        [Description("Only this Application's Episodes.")] Guid? applicationId = null,
        [Description("Only this Service's Episodes.")] Guid? serviceId = null,
        [Description("States to include: Open, Quieted, Solved, Muted. Defaults to Open alone.")]
        string[]? states = null,
        [Description("Only Episodes whose last match is at or after this ISO-8601 instant.")]
        string? since = null,
        [Description("How many to return, newest first. Default 50, at most 200.")]
        int? limit = null)
    {
        var wanted = ReadStates(states);
        var visible = await readVisibility.GetVisibleApplicationsAsync(cancellationToken);

        var query = dbContext.Episodes.AsNoTracking().AsQueryable();
        if (visible is not null)
        {
            var ids = visible.ToArray();
            query = query.Where(e => ids.Contains(e.ApplicationId));
        }

        if (applicationId is { } application)
        {
            query = query.Where(e => e.ApplicationId == new ApplicationId(application));
        }

        if (serviceId is { } service)
        {
            query = query.Where(e => e.ServiceId == new SharedKernel.ServiceId(service));
        }

        if (since is not null)
        {
            if (!DateTimeOffset.TryParse(since, out var lowerBound))
            {
                throw new McpException($"'{since}' is not an ISO-8601 instant.");
            }

            query = query.Where(e => e.LastMatchAt >= lowerBound);
        }

        var take = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var episodes = await query
            .OrderByDescending(e => e.LastMatchAt)
            .Take(take + 1)
            .ToListAsync(cancellationToken);

        // State is computed rather than stored, so the filter lands here rather than in SQL.
        var matching = episodes.Where(e => wanted.Contains(e.State)).ToList();
        var more = matching.Count > take;
        var shown = matching.Take(take).Select(Summarize).ToList();

        return new EpisodeListAnswer(
            shown,
            more
                ? $"Returned the {take} most recent matching episodes; there are older ones."
                : null);
    }

    [McpServerTool(Name = "get_episode", ReadOnly = true)]
    [Description(
        "One Episode with what the list leaves out: the Journal of every human hand laid on it, " +
        "and the Reading where one was written. Use first_match_log_id with get_log_record, and " +
        "the Service and the times with search_log_records, to reach the evidence itself.")]
    public static async Task<EpisodeDetailAnswer?> GetEpisodeAsync(
        [Description("The Episode's id.")] Guid id,
        AlertingDbContext dbContext,
        IReadVisibility readVisibility,
        CancellationToken cancellationToken)
    {
        var episode = await dbContext.Episodes.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (episode is null)
        {
            return null;
        }

        // Outside the Visibility Scope it is not there: not yours to see, not yours to know about.
        var visible = await readVisibility.GetVisibleApplicationsAsync(cancellationToken);
        if (visible is not null && !visible.Contains(episode.ApplicationId))
        {
            return null;
        }

        var journal = await dbContext.JournalEntries.AsNoTracking()
            .Where(entry => entry.EpisodeId == id)
            .OrderBy(entry => entry.At)
            .Select(entry => new JournalMark(entry.Kind.ToString(), entry.At))
            .ToListAsync(cancellationToken);

        var reading = await dbContext.Readings.AsNoTracking()
            .Where(r => r.EpisodeId == id)
            .Select(r => new { r.English, r.WrittenAt, r.Model, r.FailedAt })
            .FirstOrDefaultAsync(cancellationToken);

        return new EpisodeDetailAnswer(
            Summarize(episode),
            journal,
            reading is { English: not null, WrittenAt: not null }
                ? new MachineReading(reading.English, reading.Model, reading.WrittenAt.Value, ReadingCaveat)
                : null);
    }

    /// <summary>
    /// Travels with every Reading and is not decoration. A Reading is one machine's account of the
    /// evidence, written from a slice of it; handing it to another machine without saying so is how
    /// a guess becomes a fact somewhere down the line (ADR 0032).
    /// </summary>
    private const string ReadingCaveat =
        "This is a machine's reading of the evidence, not evidence. It was generated from the " +
        "opening match, nearby log records and any recent release — it may be wrong. Verify it " +
        "against the log records before relying on it, and never report it as what happened.";

    private static EpisodeSummary Summarize(Episode episode) => new(
        episode.Id,
        episode.ApplicationId.Value,
        episode.ServiceId.Value,
        episode.Watch.ToString(),
        episode.State.ToString(),
        episode.Fingerprint,
        episode.OpenedAt,
        episode.LastMatchAt,
        episode.ClosedAt,
        episode.ErrorCount,
        episode.WarnCount,
        episode.FirstMatchLogId,
        episode.FirstMatchDetail,
        episode.AcknowledgedAt,
        episode.SolvedAt);

    private static HashSet<EpisodeState> ReadStates(string[]? states)
    {
        if (states is null or { Length: 0 })
        {
            return [EpisodeState.Open];
        }

        var wanted = new HashSet<EpisodeState>();
        foreach (var name in states)
        {
            if (!Enum.TryParse<EpisodeState>(name, ignoreCase: true, out var state))
            {
                throw new McpException(
                    $"'{name}' is not an episode state. Use one of: " +
                    $"{string.Join(", ", Enum.GetNames<EpisodeState>())}.");
            }

            wanted.Add(state);
        }

        return wanted;
    }
}
