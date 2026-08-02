using Bugler.Alerting.CloseQuietEpisodes;
using Bugler.Alerting.Episodes;

namespace Bugler.Alerting.Tests;

public class CloseDecisionTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    [Fact]
    public void A_recently_matching_episode_stays_open()
    {
        Assert.Null(CloseDecision.Decide(
            watchOff: false, acknowledged: false, Now.AddMinutes(-5), Window, Now));
    }

    [Fact]
    public void A_full_quiet_window_closes_with_an_all_clear_owed()
    {
        Assert.Equal(EpisodeCloseReason.QuietWindow, CloseDecision.Decide(
            watchOff: false, acknowledged: false, Now - Window, Window, Now));
    }

    [Fact]
    public void A_watch_turned_off_silences_immediately_even_mid_trouble()
    {
        Assert.Equal(EpisodeCloseReason.WatchOff, CloseDecision.Decide(
            watchOff: true, acknowledged: false, Now.AddSeconds(-1), Window, Now));
    }

    [Fact]
    public void An_acknowledged_episode_never_quiets_however_long_the_silence()
    {
        Assert.Null(CloseDecision.Decide(
            watchOff: false, acknowledged: true, Now.AddDays(-30), Window, Now));
    }

    [Fact]
    public void A_watch_turned_off_outranks_the_acknowledgement()
    {
        Assert.Equal(EpisodeCloseReason.WatchOff, CloseDecision.Decide(
            watchOff: true, acknowledged: true, Now.AddSeconds(-1), Window, Now));
    }
}
