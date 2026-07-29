using Bugler.Alerting.DeliverMessages;

namespace Bugler.Alerting.Tests;

public class RetryScheduleTests
{
    [Fact]
    public void The_backoff_doubles_and_caps_at_fifteen_minutes()
    {
        Assert.Equal(TimeSpan.FromMinutes(1), RetrySchedule.NextDelay(1));
        Assert.Equal(TimeSpan.FromMinutes(2), RetrySchedule.NextDelay(2));
        Assert.Equal(TimeSpan.FromMinutes(4), RetrySchedule.NextDelay(3));
        Assert.Equal(TimeSpan.FromMinutes(8), RetrySchedule.NextDelay(4));
        Assert.Equal(TimeSpan.FromMinutes(15), RetrySchedule.NextDelay(5));
        Assert.Equal(TimeSpan.FromMinutes(15), RetrySchedule.NextDelay(50));
    }

    [Fact]
    public void A_nonsensical_attempt_count_still_yields_the_first_delay()
    {
        Assert.Equal(TimeSpan.FromMinutes(1), RetrySchedule.NextDelay(0));
    }
}
