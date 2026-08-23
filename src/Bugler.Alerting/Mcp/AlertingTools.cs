using System.ComponentModel;
using Bugler.Access.Contracts;
using Bugler.Alerting.Deliveries;
using Bugler.Alerting.Episodes;
using Bugler.Alerting.Settings;
using Bugler.Mail;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Bugler.Alerting.Mcp;

/// <summary>
/// What Alerting answers at the machine door: the Episodes, and the machine hand — the narrow
/// verbs by which an agent claims an Episode, annotates it, proposes Solved, or resigns. The
/// verbs are refused without the machine-hand grade (ADR 0029 as revised), and Solved itself
/// stays a human verdict: the machine proposes, a person confirms in the UI.
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

    // Machine-facing English, outside the catalogues (ADR 0024): the reader is an agent
    // relaying it onward, and each refusal says what stood in the way and what to do.
    private const string NoSuchEpisode = "There is no such episode.";

    private const string NotAMachine =
        "This tool answers only to a machine delegation presented at the machine door.";

    private const string ReadingGradeOnly =
        "This machine delegation was issued for reading alone. The machine hand — claim_episode, "
        + "release_claim, annotate_episode, propose_solved, resign_episode — needs a delegation "
        + "issued with the machine-hand grade. Ask its holder to issue one in Bugler under their "
        + "account.";

    [McpServerTool(Name = "list_episodes", ReadOnly = true)]
    [Description(
        "The Episodes in view: one per kind of trouble in one episode scope, opened when a "
        + "service started logging that trouble or stopped answering that it is alive, and closed "
        + "by a Quiet Window of silence. One episode may be fed by several services and versions "
        + "— its participations say which. Start here: this is what Bugler noticed unasked. "
        + "States: Open (still happening), Quieted (went quiet on its own), Solved (a person said "
        + "so), Muted. Defaults to the Episodes that are still open. Answers carry any machine "
        + "hand marks — claims, notes, proposals, resignations — so agents see each other's "
        + "work. Pass a fingerprint (with every state) to read the history of one kind of "
        + "trouble before retrying a fix that may already have failed: episodes never reopen, "
        + "and a resignation in the history means a machine already gave this kind up.")]
    public static async Task<EpisodeListAnswer> ListEpisodesAsync(
        AlertingDbContext dbContext,
        IReadVisibility readVisibility,
        IMachineDelegationNames delegationNames,
        CancellationToken cancellationToken,
        [Description("Only this Application's Episodes.")] Guid? applicationId = null,
        [Description("Only this Service's Episodes.")] Guid? serviceId = null,
        [Description("States to include: Open, Quieted, Solved, Muted. Defaults to Open alone.")]
        string[]? states = null,
        [Description(
            "Only Episodes of this kind of trouble — the fingerprint exactly as an earlier "
            + "answer carried it. It is an opaque token, not a sentence: it stands for the "
            + "trouble rather than describing it, and the title is what it stands for. It tells "
            + "kinds apart within one episode scope, so episodes of two applications never share "
            + "one.")]
        string? fingerprint = null,
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
            // An Episode has no single Service: it belongs to everything that fed it, so
            // "this Service's episodes" are the ones it put something into.
            var id = new SharedKernel.ServiceId(service);
            query = query.Where(e => dbContext.Participations.Any(
                p => p.EpisodeId == e.Id && p.ServiceId == id));
        }

        if (fingerprint is not null)
        {
            query = query.Where(e => e.Fingerprint == fingerprint);
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
        var shown = matching.Take(take).ToList();

        var names = await NamesFor(shown, delegationNames, cancellationToken);
        var overtaken = await OvertakenOf(dbContext, shown, cancellationToken);

        var participations = await ParticipationsOfAsync(dbContext, shown, cancellationToken);

        return new EpisodeListAnswer(
            shown.Select(e => Summarize(e, names, overtaken.Contains(e.Id), participations))
                .ToList(),
            more
                ? $"Returned the {take} most recent matching episodes; there are older ones."
                : null);
    }

    [McpServerTool(Name = "get_episode", ReadOnly = true)]
    [Description(
        "One Episode with what the list leaves out: the Journal of every hand laid on it — "
        + "flesh or machine — and the Reading where one was written. Use first_match_log_id "
        + "with get_log_record, and the Service and the times with search_log_records, to reach "
        + "the evidence itself.")]
    public static async Task<EpisodeDetailAnswer?> GetEpisodeAsync(
        [Description("The Episode's id.")] Guid id,
        AlertingDbContext dbContext,
        IReadVisibility readVisibility,
        IMachineDelegationNames delegationNames,
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

        var names = await NamesFor([episode], delegationNames, cancellationToken);
        var overtaken = await OvertakenOf(dbContext, [episode], cancellationToken);

        var participations = await ParticipationsOfAsync(dbContext, [episode], cancellationToken);

        return new EpisodeDetailAnswer(
            Summarize(episode, names, overtaken.Contains(episode.Id), participations),
            journal,
            reading is { English: not null, WrittenAt: not null }
                ? new MachineReading(reading.English, reading.Model, reading.WrittenAt.Value, ReadingCaveat)
                : null);
    }

    [McpServerTool(Name = "claim_episode", Destructive = false, Idempotent = true)]
    [Description(
        "Lay — or renew — this delegation's claim on an Episode: a visible, exclusive-among-"
        + "machines hold that keeps it from quieting while the work runs, exactly as a human "
        + "acknowledgement would. The claim is a lease: it wilts unless a machine write renews "
        + "it, so a crashed agent never leaves a zombie. It lands only on an open Episode that "
        + "is the newest of its kind, carries no human acknowledgement, no other machine's "
        + "claim and no standing resignation. Answers the Episode as it now stands.")]
    public static async Task<EpisodeSummary> ClaimEpisodeAsync(
        [Description("The Episode's id.")] Guid id,
        AlertingDbContext dbContext,
        IReadVisibility readVisibility,
        IMachineActor machineActor,
        IMachineDelegationNames delegationNames,
        CancellationToken cancellationToken)
    {
        var actor = RequireMachineHand(machineActor);
        var episode = await LoadEpisodeAsync(id, dbContext, readVisibility, cancellationToken);
        await RequireNewestAsync(dbContext, episode, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var lease = await LeaseOfAsync(dbContext, episode.ApplicationId, cancellationToken);
        var outcome = episode.Claim(actor.DelegationId, actor.UserId, now, lease);
        await RefuseIfStood(outcome, episode, delegationNames, cancellationToken);

        Journal(dbContext, episode, actor,
            outcome == MachineHandOutcome.Renewed
                ? JournalEntryKind.ClaimRenewed
                : JournalEntryKind.Claimed,
            now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await AnswerAsync(dbContext, episode, delegationNames, cancellationToken);
    }

    [McpServerTool(Name = "release_claim", Destructive = false, Idempotent = true)]
    [Description(
        "Give the Episode back deliberately: the claim ends, the Episode returns to its normal "
        + "lifecycle, and another machine may claim it. Releasing a claim this delegation does "
        + "not hold changes nothing and answers all the same.")]
    public static async Task<EpisodeSummary> ReleaseClaimAsync(
        [Description("The Episode's id.")] Guid id,
        AlertingDbContext dbContext,
        IReadVisibility readVisibility,
        IMachineActor machineActor,
        IMachineDelegationNames delegationNames,
        CancellationToken cancellationToken)
    {
        var actor = RequireMachineHand(machineActor);
        var episode = await LoadEpisodeAsync(id, dbContext, readVisibility, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        if (episode.ReleaseClaim(actor.DelegationId) == MachineHandOutcome.Acted)
        {
            Journal(dbContext, episode, actor, JournalEntryKind.ClaimReleased, now);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return await AnswerAsync(dbContext, episode, delegationNames, cancellationToken);
    }

    [McpServerTool(Name = "annotate_episode", Destructive = false, Idempotent = true)]
    [Description(
        "Pin the claim-holder's note on the Episode: free text, a link, or both — the place to "
        + "say what was found or where the work lives. One note per Episode; pinning again "
        + "replaces it. A machine write: the claim's lease runs anew. Claim-holder only.")]
    public static async Task<EpisodeSummary> AnnotateEpisodeAsync(
        [Description("The Episode's id.")] Guid id,
        AlertingDbContext dbContext,
        IReadVisibility readVisibility,
        IMachineActor machineActor,
        IMachineDelegationNames delegationNames,
        CancellationToken cancellationToken,
        [Description("The note's text, up to 2000 characters.")] string? note = null,
        [Description("A link worth pinning — the branch, the run, the PR.")] string? link = null)
    {
        var actor = RequireMachineHand(machineActor);
        var text = Trimmed(note);
        var url = Trimmed(link);
        if (text is null && url is null)
        {
            throw new McpException("A note needs text, a link, or both.");
        }

        if (text is { Length: > Episode.MaxMachineTextLength })
        {
            throw new McpException(
                $"The note's text can hold at most {Episode.MaxMachineTextLength} characters.");
        }

        RequireLink(url);

        var episode = await LoadEpisodeAsync(id, dbContext, readVisibility, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var lease = await LeaseOfAsync(dbContext, episode.ApplicationId, cancellationToken);
        var outcome = episode.PinNote(actor.DelegationId, text, url, now, lease);
        await RefuseIfStood(outcome, episode, delegationNames, cancellationToken);

        Journal(dbContext, episode, actor, JournalEntryKind.NotePinned, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await AnswerAsync(dbContext, episode, delegationNames, cancellationToken);
    }

    [McpServerTool(Name = "propose_solved", Destructive = false, Idempotent = true)]
    [Description(
        "Lay the claim-holder's Solved Proposal: the stated belief that the cause is fixed, "
        + "with the PR that fixes it. Solved itself stays a human verdict — a person confirms "
        + "or rejects the proposal in Bugler's UI, with the matches that arrived since it was "
        + "laid in view. New matches do not invalidate it (a merged PR takes time to deploy); "
        + "they just age it visibly. Proposing again replaces this delegation's earlier "
        + "proposal. A machine write: the claim's lease runs anew. Claim-holder only.")]
    public static async Task<EpisodeSummary> ProposeSolvedAsync(
        [Description("The Episode's id.")] Guid id,
        [Description("The PR that fixes the cause — an absolute http(s) link.")] string prLink,
        AlertingDbContext dbContext,
        IReadVisibility readVisibility,
        IMachineActor machineActor,
        IMachineDelegationNames delegationNames,
        CancellationToken cancellationToken)
    {
        var actor = RequireMachineHand(machineActor);
        var url = Trimmed(prLink)
            ?? throw new McpException("A proposal carries the PR link that fixes the cause.");
        RequireLink(url);

        var episode = await LoadEpisodeAsync(id, dbContext, readVisibility, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var lease = await LeaseOfAsync(dbContext, episode.ApplicationId, cancellationToken);
        var outcome = episode.ProposeSolved(actor.DelegationId, url, now, lease);
        await RefuseIfStood(outcome, episode, delegationNames, cancellationToken);

        Journal(dbContext, episode, actor, JournalEntryKind.ProposalLaid, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await AnswerAsync(dbContext, episode, delegationNames, cancellationToken);
    }

    [McpServerTool(Name = "resign_episode", Destructive = false, Idempotent = true)]
    [Description(
        "The machine's statement about itself: this trouble is not one it can fix from the code "
        + "— a certificate that expired, a disk that filled, a third party that fails — said "
        + "with the reason why. The claim ends with it, the subscribers and the application's "
        + "chat are told a human hand is needed, and no machine claims this Episode again until "
        + "a person sweeps the resignation aside. Resign when the fix is not yours to make; "
        + "release_claim when you simply stop working. Claim-holder only.")]
    public static async Task<EpisodeSummary> ResignEpisodeAsync(
        [Description("The Episode's id.")] Guid id,
        [Description("Why this is not a machine's to fix, up to 2000 characters.")] string reason,
        AlertingDbContext dbContext,
        IReadVisibility readVisibility,
        IMachineActor machineActor,
        IMachineDelegationNames delegationNames,
        ISmtpSettingsSource smtpSettings,
        CancellationToken cancellationToken)
    {
        var actor = RequireMachineHand(machineActor);
        var why = Trimmed(reason)
            ?? throw new McpException(
                "A resignation carries its reason: say why this is not a machine's to fix, or "
                + "nobody can act on it.");
        if (why.Length > Episode.MaxMachineTextLength)
        {
            throw new McpException(
                $"The reason can hold at most {Episode.MaxMachineTextLength} characters.");
        }

        var episode = await LoadEpisodeAsync(id, dbContext, readVisibility, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var outcome = episode.Resign(actor.DelegationId, why, now);
        await RefuseIfStood(outcome, episode, delegationNames, cancellationToken);

        Journal(dbContext, episode, actor, JournalEntryKind.Resigned, now);

        // The one machine mark that speaks: a proposal's PR notifies the code side by itself,
        // a resignation has nobody to notify but the people the trouble already concerns.
        var mailEnabled = (await smtpSettings.GetCurrentAsync(cancellationToken)).IsConfigured;
        var chatConfigured = await dbContext.ApplicationSettings
            .Where(s => s.ApplicationId == episode.ApplicationId)
            .Select(s => s.ChatWebhookUrl != null)
            .FirstOrDefaultAsync(cancellationToken);
        await ResignationsOwed.EnqueueAsync(
            dbContext, episode, mailEnabled, chatConfigured, now, cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        return await AnswerAsync(dbContext, episode, delegationNames, cancellationToken);
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

    private static MachineActor RequireMachineHand(IMachineActor machineActor)
    {
        var actor = machineActor.GetCurrent() ?? throw new McpException(NotAMachine);
        return actor.HoldsMachineHand ? actor : throw new McpException(ReadingGradeOnly);
    }

    /// <summary>Invisible reads as absent (ADR 0032): not yours to see, not yours to know about.</summary>
    private static async Task<Episode> LoadEpisodeAsync(
        Guid id,
        AlertingDbContext dbContext,
        IReadVisibility readVisibility,
        CancellationToken cancellationToken)
    {
        var episode = await dbContext.Episodes
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            ?? throw new McpException(NoSuchEpisode);

        var visible = await readVisibility.GetVisibleApplicationsAsync(cancellationToken);
        return visible is null || visible.Contains(episode.ApplicationId)
            ? episode
            : throw new McpException(NoSuchEpisode);
    }

    private static async Task RequireNewestAsync(
        AlertingDbContext dbContext, Episode episode, CancellationToken cancellationToken)
    {
        var newerExists = await dbContext.Episodes.AnyAsync(n =>
            n.ScopeKey == episode.ScopeKey
            && n.Fingerprint == episode.Fingerprint
            && n.Id.CompareTo(episode.Id) > 0, cancellationToken);
        if (newerExists)
        {
            throw new McpException(
                "This episode has been overtaken by a newer one of its kind; the claim belongs "
                + "to the newest. List episodes with this fingerprint to find it.");
        }
    }

    /// <summary>Each refusal says what stood in the way — the reader cannot see the screen.</summary>
    private static async Task RefuseIfStood(
        MachineHandOutcome outcome,
        Episode episode,
        IMachineDelegationNames delegationNames,
        CancellationToken cancellationToken)
    {
        switch (outcome)
        {
            case MachineHandOutcome.RefusedClosed:
                throw new McpException("This episode is no longer open; a claim holds nothing shut.");
            case MachineHandOutcome.RefusedSolved:
                throw new McpException("This episode is solved; the verdict has been rendered.");
            case MachineHandOutcome.RefusedAcknowledged:
                throw new McpException(
                    "A person has this kind of trouble on — the machine never touches a "
                    + "human-acknowledged episode.");
            case MachineHandOutcome.RefusedResigned:
                throw new McpException(
                    "A resignation stands on this episode: a machine already said this is not "
                    + "one it can fix. Only a person may sweep it aside.");
            case MachineHandOutcome.RefusedHeldByAnother:
                throw new McpException(await HeldByAnotherAsync(episode, delegationNames, cancellationToken));
            case MachineHandOutcome.RefusedNotHolder:
                throw new McpException(episode.ClaimedByDelegationId is null
                    ? "Nobody holds a claim on this episode; claim_episode first."
                    : await HeldByAnotherAsync(episode, delegationNames, cancellationToken));
        }
    }

    private static async Task<string> HeldByAnotherAsync(
        Episode episode,
        IMachineDelegationNames delegationNames,
        CancellationToken cancellationToken)
    {
        var holder = episode.ClaimedByDelegationId!.Value;
        var names = await delegationNames.ResolveAsync([holder], cancellationToken);
        var who = names.TryGetValue(holder, out var name)
            ? $"'{name.Name}' held by {name.HolderEmail}"
            : "another machine delegation";
        var until = episode.ClaimLeaseUntil?.ToString("O") ?? "";
        return $"Another machine holds this episode: {who}, lease until {until}. Claims are "
            + "exclusive among machines; the claim may be released, lapse, or a person may "
            + "withdraw it.";
    }

    /// <summary>Any machine write renews the lease, so its span is read where the write lands.</summary>
    private static async Task<TimeSpan> LeaseOfAsync(
        AlertingDbContext dbContext, ApplicationId applicationId, CancellationToken cancellationToken)
    {
        var hours = await dbContext.ApplicationSettings
            .Where(s => s.ApplicationId == applicationId)
            .Select(s => s.ClaimLeaseHours)
            .FirstOrDefaultAsync(cancellationToken);
        return TimeSpan.FromHours(hours ?? AlertingDefaults.ClaimLeaseHours);
    }

    private static void Journal(
        AlertingDbContext dbContext,
        Episode episode,
        MachineActor actor,
        JournalEntryKind kind,
        DateTimeOffset now) =>
        dbContext.JournalEntries.Add(new JournalEntry
        {
            EpisodeId = episode.Id,
            Kind = kind,
            UserId = actor.UserId,
            DelegationId = actor.DelegationId,
            At = now,
        });

    /// <summary>Every write answers the Episode as it now stands — the agent needs no second call.</summary>
    private static async Task<EpisodeSummary> AnswerAsync(
        AlertingDbContext dbContext,
        Episode episode,
        IMachineDelegationNames delegationNames,
        CancellationToken cancellationToken)
    {
        var names = await NamesFor([episode], delegationNames, cancellationToken);
        var overtaken = await OvertakenOf(dbContext, [episode], cancellationToken);
        var participations = await ParticipationsOfAsync(dbContext, [episode], cancellationToken);
        return Summarize(episode, names, overtaken.Contains(episode.Id), participations);
    }

    /// <summary>Which Services and versions fed each of these Episodes — one query for the page.</summary>
    private static async Task<Dictionary<Guid, IReadOnlyList<ParticipationMark>>> ParticipationsOfAsync(
        AlertingDbContext dbContext,
        IReadOnlyList<Episode> episodes,
        CancellationToken cancellationToken)
    {
        var ids = episodes.Select(e => e.Id).ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var rows = await dbContext.Participations.AsNoTracking()
            .Where(p => ids.Contains(p.EpisodeId))
            .OrderBy(p => p.FirstAt)
            .Select(p => new
            {
                p.EpisodeId,
                Mark = new ParticipationMark(
                    p.ServiceId.Value, p.Version, p.FirstAt, p.LastAt, p.ErrorCount, p.WarnCount),
            })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(row => row.EpisodeId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ParticipationMark>)group.Select(row => row.Mark).ToList());
    }

    private static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void RequireLink(string? url)
    {
        if (url is null)
        {
            return;
        }

        if (url.Length > Episode.MaxMachineLinkLength
            || !Uri.TryCreate(url, UriKind.Absolute, out var parsed)
            || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
        {
            throw new McpException(
                $"The link must be an absolute http(s) URL of at most "
                + $"{Episode.MaxMachineLinkLength} characters.");
        }
    }

    private static Task<IReadOnlyDictionary<Guid, MachineDelegationName>> NamesFor(
        IReadOnlyList<Episode> episodes,
        IMachineDelegationNames delegationNames,
        CancellationToken cancellationToken) =>
        delegationNames.ResolveAsync(
            episodes.SelectMany(DelegationIdsOf).ToHashSet(), cancellationToken);

    private static IEnumerable<Guid> DelegationIdsOf(Episode episode)
    {
        if (episode.ClaimedByDelegationId is { } claim)
        {
            yield return claim;
        }

        if (episode.NoteByDelegationId is { } note)
        {
            yield return note;
        }

        if (episode.ProposalByDelegationId is { } proposal)
        {
            yield return proposal;
        }

        if (episode.ResignedByDelegationId is { } resignation)
        {
            yield return resignation;
        }
    }

    /// <summary>Which of these Episodes a newer sibling of their kind has overtaken — asked only where a mark ages.</summary>
    private static async Task<HashSet<Guid>> OvertakenOf(
        AlertingDbContext dbContext,
        IReadOnlyList<Episode> episodes,
        CancellationToken cancellationToken)
    {
        var marked = episodes
            .Where(e => e.ProposedAt is not null || e.ResignedAt is not null)
            .Select(e => e.Id)
            .ToList();
        if (marked.Count == 0)
        {
            return [];
        }

        var ids = await dbContext.Episodes.AsNoTracking()
            .Where(e => marked.Contains(e.Id) && dbContext.Episodes.Any(p =>
                p.ScopeKey == e.ScopeKey
                && p.Fingerprint == e.Fingerprint
                && p.Id.CompareTo(e.Id) > 0))
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);
        return ids.ToHashSet();
    }

    private static EpisodeSummary Summarize(
        Episode episode,
        IReadOnlyDictionary<Guid, MachineDelegationName> names,
        bool overtaken,
        IReadOnlyDictionary<Guid, IReadOnlyList<ParticipationMark>> participations) => new(
        episode.Id,
        episode.ApplicationId.Value,
        episode.OpenedByServiceId?.Value,
        episode.Watch.ToString(),
        episode.State.ToString(),
        episode.Fingerprint,
        episode.Title,
        participations.GetValueOrDefault(episode.Id, []),
        episode.OpenedAt,
        episode.LastMatchAt,
        episode.ClosedAt,
        episode.ErrorCount,
        episode.WarnCount,
        episode.FirstMatchLogId,
        episode.FirstMatchDetail,
        episode.AcknowledgedAt,
        episode.SolvedAt,
        episode is { ClaimedByDelegationId: { } claimBy, ClaimedAt: { } claimAt, ClaimLeaseUntil: { } leaseUntil }
            ? new ClaimMark(
                NameOf(names, claimBy)?.Name, NameOf(names, claimBy)?.HolderEmail, claimAt, leaseUntil)
            : null,
        episode is { NoteByDelegationId: { } noteBy, NotedAt: { } notedAt }
            ? new NoteMark(episode.NoteText, episode.NoteLink, notedAt, NameOf(names, noteBy)?.Name)
            : null,
        episode is { ProposalByDelegationId: { } proposalBy, ProposedAt: { } proposedAt }
            ? new ProposalMark(
                episode.ProposalLink,
                proposedAt,
                Math.Max(
                    0,
                    episode.ErrorCount + episode.WarnCount - (episode.ProposalMatchesWhenLaid ?? 0)),
                overtaken,
                NameOf(names, proposalBy)?.Name)
            : null,
        episode is { ResignedByDelegationId: { } resignedBy, ResignedAt: { } resignedAt }
            ? new ResignationMark(
                episode.ResignationReason ?? "", resignedAt, overtaken, NameOf(names, resignedBy)?.Name)
            : null);

    private static MachineDelegationName? NameOf(
        IReadOnlyDictionary<Guid, MachineDelegationName> names, Guid id) =>
        names.TryGetValue(id, out var name) ? name : null;

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
