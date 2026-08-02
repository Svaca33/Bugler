using Bugler.Alerting.WatchHealthChecks;

namespace Bugler.Alerting.Tests;

/// <summary>The Health Check Watch's rule: three failures buy an Episode, one success wipes the debt.</summary>
public class HealthWatchDecisionTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void One_or_two_failures_are_not_yet_worth_waking_anybody(int failures)
    {
        Assert.Equal(
            HealthAction.None,
            HealthWatchDecision.Decide(alive: false, failures, episodeOpen: false));
    }

    [Fact]
    public void The_third_failure_in_a_row_opens_the_episode()
    {
        Assert.Equal(
            HealthAction.Open,
            HealthWatchDecision.Decide(alive: false, consecutiveFailures: 3, episodeOpen: false));
    }

    [Fact]
    public void A_failure_while_the_episode_is_open_is_a_match_without_waiting_for_three()
    {
        // The Alert has gone out; there is no longer a false alarm to guard against.
        Assert.Equal(
            HealthAction.Feed,
            HealthWatchDecision.Decide(alive: false, consecutiveFailures: 1, episodeOpen: true));
    }

    [Fact]
    public void An_answering_service_records_nothing_even_with_an_episode_open()
    {
        // The matches simply stop; what closes the Episode is the Quiet Window, not this.
        Assert.Equal(
            HealthAction.None,
            HealthWatchDecision.Decide(alive: true, consecutiveFailures: 0, episodeOpen: true));
    }
}
