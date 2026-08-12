using Bugler.Alerting.Episodes;
using Bugler.SharedKernel;

namespace Bugler.Alerting.Tests;

/// <summary>
/// The machine hand's rules (Alerting ADR 0010): the claim is a lease and exclusive among
/// machines, the human hand always wins, and the Resignation bars machines until a person
/// sweeps it aside. Solved stays a human verdict — the aggregate has no machine verb for it.
/// </summary>
public class MachineHandTests
{
    private static readonly Guid Agent = Guid.NewGuid();
    private static readonly Guid AgentUser = Guid.NewGuid();
    private static readonly Guid OtherAgent = Guid.NewGuid();
    private static readonly Guid OtherAgentUser = Guid.NewGuid();
    private static readonly Guid Dev = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 8, 11, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lease = TimeSpan.FromHours(24);

    private static Episode Episode() => new()
    {
        Id = Guid.CreateVersion7(),
        ServiceId = ServiceId.New(),
        ApplicationId = ApplicationId.New(),
        Watch = Watch.Logs,
        Fingerprint = "Payment gateway timed out",
        OpenedAt = Now.AddHours(-1),
        FirstMatchLogId = 42,
        FirstMatchAt = Now.AddHours(-1),
        FirstMatchSeverity = 17,
        ErrorCount = 3,
        WarnCount = 1,
        LastMatchAt = Now.AddMinutes(-10),
    };

    private static Episode Claimed()
    {
        var episode = Episode();
        episode.Claim(Agent, AgentUser, Now, Lease);
        return episode;
    }

    [Fact]
    public void A_claim_records_the_delegation_its_user_and_the_lease()
    {
        var episode = Episode();

        Assert.Equal(MachineHandOutcome.Acted, episode.Claim(Agent, AgentUser, Now, Lease));
        Assert.Equal(Agent, episode.ClaimedByDelegationId);
        Assert.Equal(AgentUser, episode.ClaimedByUserId);
        Assert.Equal(Now, episode.ClaimedAt);
        Assert.Equal(Now + Lease, episode.ClaimLeaseUntil);
    }

    [Fact]
    public void The_holder_reclaiming_renews_the_lease_and_keeps_the_moment()
    {
        var episode = Claimed();

        Assert.Equal(
            MachineHandOutcome.Renewed,
            episode.Claim(Agent, AgentUser, Now.AddHours(20), Lease));
        Assert.Equal(Now, episode.ClaimedAt);
        Assert.Equal(Now.AddHours(20) + Lease, episode.ClaimLeaseUntil);
    }

    [Fact]
    public void Claims_are_exclusive_among_machines()
    {
        var episode = Claimed();

        Assert.Equal(
            MachineHandOutcome.RefusedHeldByAnother,
            episode.Claim(OtherAgent, OtherAgentUser, Now.AddMinutes(1), Lease));
        Assert.Equal(Agent, episode.ClaimedByDelegationId);
    }

    [Fact]
    public void A_closed_a_solved_and_an_acknowledged_episode_take_no_claim()
    {
        var quieted = Episode();
        quieted.ClosedAt = Now.AddMinutes(-5);
        quieted.CloseReason = EpisodeCloseReason.QuietWindow;
        Assert.Equal(
            MachineHandOutcome.RefusedClosed, quieted.Claim(Agent, AgentUser, Now, Lease));

        var solved = Episode();
        solved.Solve(Dev, Now);
        Assert.Equal(
            MachineHandOutcome.RefusedSolved, solved.Claim(Agent, AgentUser, Now, Lease));

        var acknowledged = Episode();
        acknowledged.Acknowledge(Dev, Now);
        Assert.Equal(
            MachineHandOutcome.RefusedAcknowledged,
            acknowledged.Claim(Agent, AgentUser, Now, Lease));
    }

    [Fact]
    public void The_human_hand_always_wins_an_acknowledgement_displaces_the_claim()
    {
        var episode = Claimed();

        Assert.Equal(HandOutcome.Acted, episode.Acknowledge(Dev, Now.AddMinutes(1)));
        Assert.Null(episode.ClaimedByDelegationId);
        Assert.Null(episode.ClaimLeaseUntil);
    }

    [Fact]
    public void Releasing_is_the_holders_alone_and_releasing_nothing_is_nothing()
    {
        var episode = Claimed();

        Assert.Equal(MachineHandOutcome.Nothing, episode.ReleaseClaim(OtherAgent));
        Assert.Equal(Agent, episode.ClaimedByDelegationId);

        Assert.Equal(MachineHandOutcome.Acted, episode.ReleaseClaim(Agent));
        Assert.Null(episode.ClaimedByDelegationId);
        Assert.Equal(MachineHandOutcome.Nothing, episode.ReleaseClaim(Agent));
    }

    [Fact]
    public void Shedding_says_whose_claim_fell_for_the_journal_line()
    {
        var episode = Claimed();

        Assert.Equal((Agent, AgentUser), episode.ShedClaim());
        Assert.Null(episode.ShedClaim());
    }

    [Fact]
    public void The_note_and_the_proposal_belong_to_the_claim_holder()
    {
        var episode = Episode();
        Assert.Equal(
            MachineHandOutcome.RefusedNotHolder,
            episode.PinNote(Agent, "found it", null, Now, Lease));
        Assert.Equal(
            MachineHandOutcome.RefusedNotHolder,
            episode.ProposeSolved(Agent, "https://example.test/pr/1", Now, Lease));
        Assert.Equal(
            MachineHandOutcome.RefusedNotHolder, episode.Resign(Agent, "not code", Now));

        var claimed = Claimed();
        Assert.Equal(
            MachineHandOutcome.RefusedNotHolder,
            claimed.PinNote(OtherAgent, "mine now", null, Now, Lease));
    }

    [Fact]
    public void A_machine_write_renews_the_lease()
    {
        var episode = Claimed();

        episode.PinNote(Agent, "found it", "https://example.test/branch", Now.AddHours(3), Lease);
        Assert.Equal(Now.AddHours(3) + Lease, episode.ClaimLeaseUntil);

        episode.ProposeSolved(Agent, "https://example.test/pr/1", Now.AddHours(5), Lease);
        Assert.Equal(Now.AddHours(5) + Lease, episode.ClaimLeaseUntil);
    }

    [Fact]
    public void The_proposal_remembers_the_match_tally_it_was_laid_at()
    {
        var episode = Claimed();

        Assert.Equal(
            MachineHandOutcome.Acted,
            episode.ProposeSolved(Agent, "https://example.test/pr/1", Now, Lease));
        Assert.Equal(4, episode.ProposalMatchesWhenLaid); // 3 errors + 1 warning at lay time.
        Assert.Equal("https://example.test/pr/1", episode.ProposalLink);
    }

    [Fact]
    public void Rejecting_the_proposal_clears_it_and_the_claim_with_it()
    {
        var episode = Claimed();
        episode.ProposeSolved(Agent, "https://example.test/pr/1", Now, Lease);

        Assert.Equal(HandOutcome.Acted, episode.RejectProposal());
        Assert.Null(episode.ProposedAt);
        Assert.Null(episode.ProposalLink);
        Assert.Null(episode.ClaimedByDelegationId);

        Assert.Equal(HandOutcome.Nothing, episode.RejectProposal());
    }

    [Fact]
    public void Resigning_ends_the_claim_and_bars_machines_until_a_person_sweeps_it_aside()
    {
        var episode = Claimed();

        Assert.Equal(MachineHandOutcome.Acted, episode.Resign(Agent, "The disk is full", Now));
        Assert.Equal(Agent, episode.ResignedByDelegationId);
        Assert.Equal("The disk is full", episode.ResignationReason);
        Assert.Null(episode.ClaimedByDelegationId);

        // No machine claims past a standing resignation — not even the one that laid it.
        Assert.Equal(
            MachineHandOutcome.RefusedResigned,
            episode.Claim(OtherAgent, OtherAgentUser, Now.AddMinutes(1), Lease));
        Assert.Equal(
            MachineHandOutcome.RefusedResigned,
            episode.Claim(Agent, AgentUser, Now.AddMinutes(1), Lease));

        Assert.Equal(HandOutcome.Acted, episode.DismissResignation());
        Assert.Null(episode.ResignedAt);
        Assert.Equal(
            MachineHandOutcome.Acted,
            episode.Claim(OtherAgent, OtherAgentUser, Now.AddMinutes(2), Lease));

        Assert.Equal(HandOutcome.Nothing, episode.DismissResignation());
    }

    [Fact]
    public void A_resignation_and_a_pending_proposal_stand_side_by_side()
    {
        // Agent A proposed and its claim lapsed; agent B claims, looks, and resigns. Both
        // machine statements stand — the person weighs them together.
        var episode = Claimed();
        episode.ProposeSolved(Agent, "https://example.test/pr/1", Now, Lease);
        episode.ShedClaim();

        Assert.Equal(
            MachineHandOutcome.Acted,
            episode.Claim(OtherAgent, OtherAgentUser, Now.AddHours(1), Lease));
        Assert.Equal(
            MachineHandOutcome.Acted,
            episode.Resign(OtherAgent, "The certificate expired", Now.AddHours(2)));

        Assert.NotNull(episode.ProposedAt);
        Assert.NotNull(episode.ResignedAt);
    }

    [Fact]
    public void The_verdict_consumes_every_machine_mark()
    {
        var episode = Claimed();
        episode.PinNote(Agent, "found it", null, Now, Lease);
        episode.ProposeSolved(Agent, "https://example.test/pr/1", Now, Lease);

        Assert.Equal(HandOutcome.Acted, episode.Solve(Dev, Now.AddMinutes(1)));

        // Confirming the proposal is this very verdict: claim and proposal are spent by it.
        Assert.Null(episode.ClaimedByDelegationId);
        Assert.Null(episode.ProposedAt);
        // The note is not a mark awaiting anyone — it stays as what the machine said.
        Assert.Equal("found it", episode.NoteText);
    }
}
