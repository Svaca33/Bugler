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
        ServiceId = ServiceId.New(),
        ApplicationId = ApplicationId.New(),
        Fingerprint = "Payment gateway timed out",
        OpenedAt = Now.AddHours(-1),
        FirstLogId = 42,
        FirstLogTimestamp = Now.AddHours(-1),
        FirstLogSeverity = 17,
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
        episode.CloseReason = EpisodeCloseReason.SensitivityOff;
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

        Assert.True(episode.Acknowledge(Dev, Now));
        Assert.Equal(Dev, episode.AcknowledgedByUserId);
        Assert.Equal(Now, episode.AcknowledgedAt);

        // One slot, last hand wins: a colleague takes it over, no ceremony.
        Assert.True(episode.Acknowledge(OtherDev, Now.AddMinutes(1)));
        Assert.Equal(OtherDev, episode.AcknowledgedByUserId);
    }

    [Fact]
    public void The_acknowledgement_survives_quieting_and_can_be_withdrawn()
    {
        var episode = Quieted();

        Assert.True(episode.Acknowledge(Dev, Now));
        Assert.Equal(EpisodeState.Quieted, episode.State);

        episode.Unacknowledge();
        Assert.Null(episode.AcknowledgedByUserId);
        Assert.Null(episode.AcknowledgedAt);
    }

    [Fact]
    public void Solving_an_open_episode_closes_it_on_the_spot()
    {
        var episode = Episode();

        Assert.True(episode.Solve(Dev, Now));

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

        Assert.True(episode.Solve(Dev, Now));

        // Quieted-then-solved stays distinguishable from solved-while-open (ADR 0003).
        Assert.Equal(EpisodeState.Solved, episode.State);
        Assert.Equal(quietedAt, episode.ClosedAt);
        Assert.Equal(EpisodeCloseReason.QuietWindow, episode.CloseReason);
    }

    [Fact]
    public void A_muted_episode_may_still_be_solved()
    {
        var episode = Muted();

        Assert.True(episode.Solve(Dev, Now));

        Assert.Equal(EpisodeState.Solved, episode.State);
        Assert.Equal(EpisodeCloseReason.SensitivityOff, episode.CloseReason);
    }

    [Fact]
    public void The_verdict_is_rendered_once()
    {
        var episode = Episode();
        Assert.True(episode.Solve(Dev, Now));

        Assert.False(episode.Solve(OtherDev, Now.AddMinutes(1)));
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

        Assert.False(episode.Acknowledge(OtherDev, Now.AddMinutes(2)));
        Assert.Null(episode.AcknowledgedByUserId);
    }
}
