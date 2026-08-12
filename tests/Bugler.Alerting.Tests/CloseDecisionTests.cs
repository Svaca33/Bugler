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
            watchOff: false, acknowledged: false, machineClaimed: false,
            Now.AddMinutes(-5), Window, Now));
    }

    [Fact]
    public void A_full_quiet_window_closes_with_an_all_clear_owed()
    {
        Assert.Equal(EpisodeCloseReason.QuietWindow, CloseDecision.Decide(
            watchOff: false, acknowledged: false, machineClaimed: false, Now - Window, Window, Now));
    }

    [Fact]
    public void A_watch_turned_off_silences_immediately_even_mid_trouble()
    {
        Assert.Equal(EpisodeCloseReason.WatchOff, CloseDecision.Decide(
            watchOff: true, acknowledged: false, machineClaimed: false,
            Now.AddSeconds(-1), Window, Now));
    }

    [Fact]
    public void An_acknowledged_episode_never_quiets_however_long_the_silence()
    {
        Assert.Null(CloseDecision.Decide(
            watchOff: false, acknowledged: true, machineClaimed: false,
            Now.AddDays(-30), Window, Now));
    }

    [Fact]
    public void A_machine_claimed_episode_never_quiets_either()
    {
        // The same hold an acknowledgement exerts (CONTEXT.md: Machine Claim) — the sweep has
        // already lapsed the expired leases, so a claim seen here is a live one.
        Assert.Null(CloseDecision.Decide(
            watchOff: false, acknowledged: false, machineClaimed: true,
            Now.AddDays(-30), Window, Now));
    }

    [Fact]
    public void A_watch_turned_off_outranks_the_acknowledgement()
    {
        Assert.Equal(EpisodeCloseReason.WatchOff, CloseDecision.Decide(
            watchOff: true, acknowledged: true, machineClaimed: false,
            Now.AddSeconds(-1), Window, Now));
    }

    [Fact]
    public void A_watch_turned_off_outranks_the_machine_claim_too()
    {
        Assert.Equal(EpisodeCloseReason.WatchOff, CloseDecision.Decide(
            watchOff: true, acknowledged: false, machineClaimed: true,
            Now.AddSeconds(-1), Window, Now));
    }
}
