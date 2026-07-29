using Bugler.Alerting.CloseQuietEpisodes;
using Bugler.Alerting.Episodes;
using Bugler.Alerting.Settings;

namespace Bugler.Alerting.Tests;

public class CloseDecisionTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    [Fact]
    public void A_recently_matching_episode_stays_open()
    {
        Assert.Null(CloseDecision.Decide(
            Sensitivity.Errors, Now.AddMinutes(-5), Window, Now));
    }

    [Fact]
    public void A_full_quiet_window_closes_with_an_all_clear_owed()
    {
        Assert.Equal(EpisodeCloseReason.QuietWindow, CloseDecision.Decide(
            Sensitivity.Errors, Now - Window, Window, Now));
    }

    [Fact]
    public void Sensitivity_off_silences_immediately_even_mid_trouble()
    {
        Assert.Equal(EpisodeCloseReason.SensitivityOff, CloseDecision.Decide(
            Sensitivity.Off, Now.AddSeconds(-1), Window, Now));
    }
}
