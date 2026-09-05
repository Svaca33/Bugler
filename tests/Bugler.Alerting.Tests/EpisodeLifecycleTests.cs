using Bugler.Alerting.DetectEpisodes;
using Bugler.Alerting.Episodes;
using Bugler.SharedKernel;

namespace Bugler.Alerting.Tests;

/// <summary>The lifecycle rules of ADR 0003: machine states, human verdicts, one-way doors.</summary>
public class EpisodeLifecycleTests
{
    private static readonly Guid Dev = Guid.NewGuid();
    private static readonly Guid OtherDev = Guid.NewGuid();
    private static readonly DateTimeOffset Now = new(2026, 7, 31, 12, 0, 0, TimeSpan.Zero);

    private static Episode Episode() => new()
    {
        Id = Guid.CreateVersion7(),
        OpenedByServiceId = ServiceId.New(),
        ApplicationId = ApplicationId.New(),
        ScopeKey = "app=test|env=prod",
        Watch = Watch.Logs,
        Fingerprint = "9f2c1b7a4d6e8051",
        Title = "Payment gateway timed out",
        RecipeVersion = Fingerprint.RecipeVersion,
        FingerprintRung = FingerprintRung.Stack,
        OpenedAt = Now.AddHours(-1),
        FirstMatchLogId = 42,
        FirstMatchAt = Now.AddHours(-1),
        FirstMatchSeverity = 17,
        ErrorCount = 3,
        WarnCount = 0,
        LastMatchAt = Now.AddMinutes(-10),
    };

    private static Episode Quieted()
    {
        var episode = Episode();
        episode.ClosedAt = Now.AddMinutes(-5);
        episode.CloseReason = EpisodeCloseReason.QuietWindow;
        return episode;
    }

    private static Episode Muted()
    {
        var episode = Episode();
        episode.ClosedAt = Now.AddMinutes(-5);
        episode.CloseReason = EpisodeCloseReason.WatchOff;
        return episode;
    }

    [Fact]
    public void The_state_is_derived_from_how_the_stretch_ended()
    {
        Assert.Equal(EpisodeState.Open, Episode().State);
        Assert.Equal(EpisodeState.Quieted, Quieted().State);
        Assert.Equal(EpisodeState.Muted, Muted().State);
    }

    [Fact]
    public void Acknowledging_records_who_and_when_and_takeover_overwrites()
    {
        var episode = Episode();

        Assert.Equal(HandOutcome.Acted, episode.Acknowledge(Dev, Now));
        Assert.Equal(Dev, episode.AcknowledgedByUserId);
        Assert.Equal(Now, episode.AcknowledgedAt);

        // One slot, last hand wins: a colleague takes it over, no ceremony.
        Assert.Equal(HandOutcome.Acted, episode.Acknowledge(OtherDev, Now.AddMinutes(1)));
        Assert.Equal(OtherDev, episode.AcknowledgedByUserId);
    }

    [Fact]
    public void The_holder_reacknowledging_is_not_an_act()
    {
        var episode = Episode();
        episode.Acknowledge(Dev, Now);

        // A no-op on both sides (ADR 0006): the mark keeps its original moment, so the live
        // timestamp never shows a moment the Journal cannot explain.
        Assert.Equal(HandOutcome.Nothing, episode.Acknowledge(Dev, Now.AddMinutes(5)));
        Assert.Equal(Now, episode.AcknowledgedAt);
    }

    [Fact]
    public void The_acknowledgement_survives_quieting_and_can_be_withdrawn()
    {
        var episode = Quieted();

        Assert.Equal(HandOutcome.Acted, episode.Acknowledge(Dev, Now));
        Assert.Equal(EpisodeState.Quieted, episode.State);

        Assert.Equal(HandOutcome.Acted, episode.Unacknowledge());
        Assert.Null(episode.AcknowledgedByUserId);
        Assert.Null(episode.AcknowledgedAt);

        // Withdrawing nothing is nothing.
        Assert.Equal(HandOutcome.Nothing, episode.Unacknowledge());
    }

    [Fact]
    public void Solving_an_open_episode_closes_it_on_the_spot()
    {
        var episode = Episode();

        Assert.Equal(HandOutcome.Acted, episode.Solve(Dev, Now));

        Assert.Equal(EpisodeState.Solved, episode.State);
        Assert.Equal(Now, episode.ClosedAt);
        Assert.Equal(EpisodeCloseReason.Solved, episode.CloseReason);
        Assert.Equal(Dev, episode.SolvedByUserId);
    }

    [Fact]
    public void Solving_a_quieted_episode_keeps_how_the_stretch_ended()
    {
        var episode = Quieted();
        var quietedAt = episode.ClosedAt;

        Assert.Equal(HandOutcome.Acted, episode.Solve(Dev, Now));

        // Quieted-then-solved stays distinguishable from solved-while-open (ADR 0003).
        Assert.Equal(EpisodeState.Solved, episode.State);
        Assert.Equal(quietedAt, episode.ClosedAt);
        Assert.Equal(EpisodeCloseReason.QuietWindow, episode.CloseReason);
    }

    [Fact]
    public void A_muted_episode_may_still_be_solved()
    {
        var episode = Muted();

        Assert.Equal(HandOutcome.Acted, episode.Solve(Dev, Now));

        Assert.Equal(EpisodeState.Solved, episode.State);
        Assert.Equal(EpisodeCloseReason.WatchOff, episode.CloseReason);
    }

    [Fact]
    public void The_verdict_is_rendered_once()
    {
        var episode = Episode();
        Assert.Equal(HandOutcome.Acted, episode.Solve(Dev, Now));

        Assert.Equal(HandOutcome.Refused, episode.Solve(OtherDev, Now.AddMinutes(1)));
        Assert.Equal(Dev, episode.SolvedByUserId);
    }

    [Fact]
    public void Solving_consumes_the_acknowledgement_and_a_solved_episode_takes_none()
    {
        var episode = Episode();
        episode.Acknowledge(Dev, Now);

        episode.Solve(Dev, Now.AddMinutes(1));
        Assert.Null(episode.AcknowledgedByUserId);
        Assert.Null(episode.AcknowledgedAt);

        Assert.Equal(HandOutcome.Refused, episode.Acknowledge(OtherDev, Now.AddMinutes(2)));
        Assert.Null(episode.AcknowledgedByUserId);
    }

    [Fact]
    public void Only_a_closed_episode_is_filed_away()
    {
        var episode = Episode();

        // An open Episode is still taking Matches; filing it would be Muting by the back door.
        Assert.Equal(HandOutcome.Refused, episode.Archive(Dev, Now));
        Assert.Null(episode.ArchivedAt);
        Assert.Null(episode.ArchivedByUserId);
    }

    [Fact]
    public void Filing_says_nothing_about_the_trouble_and_lifting_restores_it_unchanged()
    {
        var episode = Quieted();
        episode.Acknowledge(Dev, Now);

        Assert.Equal(HandOutcome.Acted, episode.Archive(OtherDev, Now.AddMinutes(1)));
        Assert.Equal(Now.AddMinutes(1), episode.ArchivedAt);
        Assert.Equal(OtherDev, episode.ArchivedByUserId);
        // A mark on top of a state, never a state of its own: Quieted still says how the stretch
        // ended, and the marks the Episode already carried are untouched.
        Assert.Equal(EpisodeState.Quieted, episode.State);
        Assert.Equal(Dev, episode.AcknowledgedByUserId);

        Assert.Equal(HandOutcome.Acted, episode.Unarchive());
        Assert.Null(episode.ArchivedAt);
        Assert.Null(episode.ArchivedByUserId);
        Assert.Equal(EpisodeState.Quieted, episode.State);
        Assert.Equal(Dev, episode.AcknowledgedByUserId);
    }

    [Fact]
    public void Filing_the_filed_and_lifting_the_unfiled_are_nothing()
    {
        var episode = Quieted();
        episode.Archive(Dev, Now);

        // The mark is shared, so a second hand has nothing to add — and the first one's moment
        // stands, the way a re-acknowledgement keeps its own.
        Assert.Equal(HandOutcome.Nothing, episode.Archive(OtherDev, Now.AddMinutes(1)));
        Assert.Equal(Dev, episode.ArchivedByUserId);
        Assert.Equal(Now, episode.ArchivedAt);

        Assert.Equal(HandOutcome.Acted, episode.Unarchive());
        Assert.Equal(HandOutcome.Nothing, episode.Unarchive());
    }

    [Fact]
    public void The_verdict_does_not_file_the_episode_away()
    {
        var episode = Episode();
        episode.Solve(Dev, Now);

        // Solved says what became of the trouble; filing says what the everyday view shows.
        Assert.Null(episode.ArchivedAt);
        Assert.Equal(HandOutcome.Acted, episode.Archive(Dev, Now.AddMinutes(1)));
        Assert.Equal(EpisodeState.Solved, episode.State);
    }
}
