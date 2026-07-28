using Bugler.Host.IntegrationEvents;

namespace Bugler.Host.Tests;

/// <summary>
/// A failed delivery retries quickly at first and gives up eventually, so a database blip
/// resolves in seconds while a lasting fault neither spins nor retries forever (ADR 0008).
/// </summary>
public class OutboxRetryPolicyTests
{
    [Fact]
    public void First_failure_retries_after_a_second() =>
        Assert.Equal(TimeSpan.FromSeconds(1), OutboxRetryPolicy.DelayAfter(1));

    [Theory]
    [InlineData(2, 2)]
    [InlineData(3, 4)]
    [InlineData(4, 8)]
    [InlineData(5, 16)]
    public void Delay_doubles_with_every_failure(int failures, int expectedSeconds) =>
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), OutboxRetryPolicy.DelayAfter(failures));

    [Fact]
    public void Delay_is_capped_so_a_lasting_fault_still_gets_retried() =>
        Assert.Equal(TimeSpan.FromMinutes(5), OutboxRetryPolicy.DelayAfter(100));

    [Fact]
    public void Delay_never_goes_below_the_first_one() =>
        Assert.Equal(TimeSpan.FromSeconds(1), OutboxRetryPolicy.DelayAfter(0));

    [Fact]
    public void Messages_are_retried_up_to_the_cap()
    {
        Assert.False(OutboxRetryPolicy.ShouldPark(1));
        Assert.False(OutboxRetryPolicy.ShouldPark(OutboxRetryPolicy.MaxAttempts - 1));
    }

    [Fact]
    public void Messages_are_parked_once_the_cap_is_reached()
    {
        Assert.True(OutboxRetryPolicy.ShouldPark(OutboxRetryPolicy.MaxAttempts));
        Assert.True(OutboxRetryPolicy.ShouldPark(OutboxRetryPolicy.MaxAttempts + 1));
    }
}
