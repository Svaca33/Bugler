using System.Net.Http.Json;
using System.Text.Json;
using Bugler.Access.ManageMachineDelegations;
using Bugler.Alerting.DescribeEpisode;
using Bugler.Alerting.Episodes;
using Bugler.Alerting.ListEpisodes;
using Bugler.Alerting.Mcp;

namespace Bugler.IntegrationTests;

/// <summary>
/// The machine hand end to end (Alerting ADR 0010): the grade gates the write verbs, a claim is
/// visible over REST, the human hand displaces it, and a Resignation bars machines until a
/// person sweeps it aside. The MCP calls go through the real door — Streamable HTTP, stateless —
/// so what is proven includes the transport the agent actually uses.
/// </summary>
public sealed class MachineHandTests : IAsyncLifetime
{
    private BuglerHarness _harness = null!;

    public async Task InitializeAsync()
    {
        _harness = await BuglerHarness.StartAsync();
        (await _harness.Client.PutAsJsonAsync(
            "/api/admin/mcp/settings",
            new { opened = true, publicUrl = "https://bugler.test/mcp" })).EnsureSuccessStatusCode();
    }

    public async Task DisposeAsync() => await _harness.DisposeAsync();

    [Fact]
    public async Task The_grade_is_stamped_at_issue_and_reading_is_the_default()
    {
        var reading = await IssueAsync("reader", grade: null);
        var hand = await IssueAsync("fixer", grade: "MachineHand");

        Assert.Equal("Reading", reading.MachineDelegation.Grade.ToString());
        Assert.Equal("MachineHand", hand.MachineDelegation.Grade.ToString());

        var listed = await _harness.Client
            .GetFromJsonAsync<List<MachineDelegationDto>>("/api/machine-delegations");
        Assert.Contains(listed!, d => d.Id == hand.MachineDelegation.Id
            && d.Grade.ToString() == "MachineHand");
    }

    [Fact]
    public async Task A_reading_delegation_is_refused_every_machine_hand_verb()
    {
        var episodeId = await SeedEpisodeAsync("boom");
        var reading = await IssueAsync("reader", grade: null);

        var refusal = await CallToolAsync(reading.Secret, "claim_episode", new { id = episodeId });

        Assert.Contains("machine-hand grade", refusal);
    }

    [Fact]
    public async Task A_claim_is_laid_seen_over_rest_and_displaced_by_the_human_hand()
    {
        var episodeId = await SeedEpisodeAsync("boom");
        var hand = await IssueAsync("fixer", grade: "MachineHand");

        var claimed = await CallToolAsync(hand.Secret, "claim_episode", new { id = episodeId });
        Assert.Contains("fixer", claimed);

        // The claim stands on the row the UI reads, named and leased.
        var detail = await DetailAsync(episodeId);
        Assert.NotNull(detail.Episode.MachineClaim);
        Assert.Equal("fixer", detail.Episode.MachineClaim!.By.Name);

        // Exclusive among machines: a second delegation is told who holds it.
        var rival = await IssueAsync("rival", grade: "MachineHand");
        var refused = await CallToolAsync(rival.Secret, "claim_episode", new { id = episodeId });
        Assert.Contains("Another machine holds this episode", refused);
        Assert.Contains("fixer", refused);

        // The human hand always wins — acknowledging displaces the claim, and the Journal
        // records the displacement naming both hands.
        (await _harness.Client.PostAsync($"/api/alerting/episodes/{episodeId}/acknowledge", null))
            .EnsureSuccessStatusCode();
        detail = await DetailAsync(episodeId);
        Assert.Null(detail.Episode.MachineClaim);
        Assert.Contains(detail.Journal, j => j.Kind == JournalEntryKind.Claimed);
        Assert.Contains(detail.Journal,
            j => j.Kind == JournalEntryKind.ClaimDisplaced && j.Machine?.Name == "fixer");
    }

    [Fact]
    public async Task The_proposal_waits_for_the_verdict_and_rejecting_clears_the_claim_too()
    {
        var episodeId = await SeedEpisodeAsync("boom");
        var hand = await IssueAsync("fixer", grade: "MachineHand");
        await CallToolAsync(hand.Secret, "claim_episode", new { id = episodeId });

        await CallToolAsync(hand.Secret, "propose_solved",
            new { id = episodeId, prLink = "https://github.test/fix/1" });

        var detail = await DetailAsync(episodeId);
        Assert.NotNull(detail.Episode.SolvedProposal);
        Assert.Equal("https://github.test/fix/1", detail.Episode.SolvedProposal!.Link);

        (await _harness.Client.PostAsync(
            $"/api/alerting/episodes/{episodeId}/proposal/reject", null)).EnsureSuccessStatusCode();

        detail = await DetailAsync(episodeId);
        Assert.Null(detail.Episode.SolvedProposal);
        Assert.Null(detail.Episode.MachineClaim);
        Assert.Contains(detail.Journal,
            j => j.Kind == JournalEntryKind.ProposalRejected && j.Machine?.Name == "fixer");
    }

    [Fact]
    public async Task A_resignation_bars_machines_until_a_person_sweeps_it_aside()
    {
        var episodeId = await SeedEpisodeAsync("disk full");
        var hand = await IssueAsync("fixer", grade: "MachineHand");
        await CallToolAsync(hand.Secret, "claim_episode", new { id = episodeId });

        await CallToolAsync(hand.Secret, "resign_episode",
            new { id = episodeId, reason = "The disk is full; no code fixes that." });

        var detail = await DetailAsync(episodeId);
        Assert.Null(detail.Episode.MachineClaim);
        Assert.NotNull(detail.Episode.Resignation);
        Assert.Equal("fixer", detail.Episode.Resignation!.By.Name);

        // Standing, it counts on the rail and refuses every machine claim.
        var counts = await _harness.Client.GetFromJsonAsync<EpisodeCountsResponse>(
            "/api/alerting/episodes/counts");
        Assert.Equal(1, counts!.Resignations);
        var refused = await CallToolAsync(hand.Secret, "claim_episode", new { id = episodeId });
        Assert.Contains("resignation stands", refused);

        // Swept aside by a person, the episode is the machines' to claim again.
        (await _harness.Client.DeleteAsync($"/api/alerting/episodes/{episodeId}/resignation"))
            .EnsureSuccessStatusCode();
        var reclaimed = await CallToolAsync(hand.Secret, "claim_episode", new { id = episodeId });
        Assert.Contains("fixer", reclaimed);

        detail = await DetailAsync(episodeId);
        Assert.Null(detail.Episode.Resignation);
        Assert.Contains(detail.Journal,
            j => j.Kind == JournalEntryKind.ResignationDismissed && j.Machine?.Name == "fixer");
    }

    [Fact]
    public async Task A_delegation_never_reaches_past_its_users_visibility()
    {
        var (foreignApp, foreignService, _) = await _harness.SeedApplicationAsync(
            "Crm", "acme", "prod", "backend");
        var foreignEpisode = await SeedEpisodeAsync("foreign trouble", foreignService, foreignApp);

        // A member granted only the harness application, with the machine hand.
        var member = await _harness.CreateUserClientAsync(
            "member@bugler.test", "MemberPass123!", _harness.ApplicationId);
        var issued = await member.PostAsJsonAsync("/api/machine-delegations",
            new { name = "members-agent", applicationId = (Guid?)null, lifetimeDays = (int?)null, grade = "MachineHand" });
        issued.EnsureSuccessStatusCode();
        var delegation = (await issued.Content.ReadFromJsonAsync<IssuedMachineDelegationDto>())!;

        var refusal = await CallToolAsync(
            delegation.Secret, "claim_episode", new { id = foreignEpisode });

        // Outside the scope it is not there: not yours to see, not yours to know about.
        Assert.Contains("no such episode", refusal);
    }

    [Fact]
    public async Task The_machine_door_lists_filed_episodes_whole_and_narrows_only_when_asked()
    {
        var filed = await SeedEpisodeAsync("filed trouble", quieted: true);
        var unfiled = await SeedEpisodeAsync("live trouble", quieted: true);
        (await _harness.Client.PostAsync($"/api/alerting/episodes/{filed}/archive", null))
            .EnsureSuccessStatusCode();
        var reading = await IssueAsync("reader", grade: null);

        // No implicit filter: a filing tidies the view a person reads, and hiding it from a
        // machine would turn it into a blindfold. The mark itself is legible in the answer.
        var whole = await CallToolAsync(
            reading.Secret, "list_episodes", new { states = new[] { "Quieted" } });
        Assert.Contains(filed.ToString(), whole);
        Assert.Contains(unfiled.ToString(), whole);
        var filedSummary = SummaryOf(whole, filed);
        var unfiledSummary = SummaryOf(whole, unfiled);
        Assert.NotNull(filedSummary.ArchivedAt);
        Assert.Null(unfiledSummary.ArchivedAt);

        var filedOnly = await CallToolAsync(
            reading.Secret, "list_episodes", new { states = new[] { "Quieted" }, archived = true });
        Assert.Contains(filed.ToString(), filedOnly);
        Assert.DoesNotContain(unfiled.ToString(), filedOnly);

        var unfiledOnly = await CallToolAsync(
            reading.Secret, "list_episodes", new { states = new[] { "Quieted" }, archived = false });
        Assert.DoesNotContain(filed.ToString(), unfiledOnly);
        Assert.Contains(unfiled.ToString(), unfiledOnly);

        // get_episode does not look away either.
        var one = await CallToolAsync(reading.Secret, "get_episode", new { id = filed });
        var detail = AnswerOf<EpisodeDetailAnswer>(one);
        Assert.Equal(filed, detail.Episode.Id);
        Assert.NotNull(detail.Episode.ArchivedAt);
    }

    private static EpisodeSummary SummaryOf(string listBody, Guid id) =>
        Assert.Single(AnswerOf<EpisodeListAnswer>(listBody).Episodes, e => e.Id == id);

    /// <summary>
    /// The typed answer out of the door's envelope: one SSE frame carrying the JSON-RPC result,
    /// whose text content is the answer serialised the way the tool wrote it.
    /// </summary>
    private static T AnswerOf<T>(string body)
    {
        var data = body.Split('\n').Single(line => line.StartsWith("data:", StringComparison.Ordinal))["data:".Length..];
        var text = JsonDocument.Parse(data).RootElement
            .GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString()!;
        return JsonSerializer.Deserialize<T>(text, JsonSerializerOptions.Web)!;
    }

    private async Task<IssuedMachineDelegationDto> IssueAsync(string name, string? grade)
    {
        var response = await _harness.Client.PostAsJsonAsync(
            "/api/machine-delegations",
            new { name, applicationId = (Guid?)null, lifetimeDays = (int?)null, grade });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IssuedMachineDelegationDto>())!;
    }

    /// <summary>
    /// One tools/call through the real machine door. The transport is stateless (ADR 0030), so a
    /// bare call needs no session dance; the raw body comes back because the refusals live in
    /// it whichever envelope — result or error — the server chose.
    /// </summary>
    private async Task<string> CallToolAsync(string secret, string tool, object arguments)
    {
        var client = _harness.CreateAnonymousClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {secret}");
        client.DefaultRequestHeaders.Add("Accept", "application/json, text/event-stream");

        var response = await client.PostAsync("/mcp", JsonContent.Create(new
        {
            jsonrpc = "2.0",
            id = 7,
            method = "tools/call",
            @params = new { name = tool, arguments },
        }));
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<EpisodeDetailDto> DetailAsync(Guid id) =>
        (await _harness.Client.GetFromJsonAsync<EpisodeDetailDto>(
            $"/api/alerting/episodes/{id}/detail"))!;

    private async Task<Guid> SeedEpisodeAsync(
        string body, Guid? serviceId = null, Guid? applicationId = null, bool quieted = false)
    {
        var id = Guid.CreateVersion7();
        await _harness.ExecuteSqlAsync(
            $"""
            INSERT INTO alerting.episodes
                (id, opened_by_service_id, application_id, scope_key, watch, fingerprint, title,
                 recipe_version, fingerprint_rung, stack_truncated, alert_folded_into_storm,
                 opened_at, first_match_log_id, first_match_at, first_match_severity,
                 first_match_detail, error_count, warn_count, last_match_at, closed_at, close_reason)
            VALUES
                ('{id}', '{serviceId ?? _harness.ServiceId}', '{applicationId ?? _harness.ApplicationId}',
                 'app={applicationId ?? _harness.ApplicationId}|env=prod', 1, '{body}', '{body}',
                 1, 2, false, false, now(), 1, now(), 17, '{body}', 1, 0, now(),
                 {(quieted ? "now()" : "NULL")}, {(quieted ? "1" : "NULL")});
            INSERT INTO alerting.participations
                (id, episode_id, service_id, version, first_at, last_at, error_count, warn_count)
            VALUES
                (gen_random_uuid(), '{id}', '{serviceId ?? _harness.ServiceId}', NULL,
                 now(), now(), 1, 0);
            """);
        return id;
    }
}
