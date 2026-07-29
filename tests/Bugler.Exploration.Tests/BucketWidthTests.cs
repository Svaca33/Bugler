using Bugler.Exploration.Querying;

namespace Bugler.Exploration.Tests;

public class BucketWidthTests
{
    [Theory]
    [InlineData(5, 0, 0, 5)]        // Last 5 min  ->  5 s ->  60 Buckets
    [InlineData(15, 0, 0, 10)]      // Last 15 min -> 10 s ->  90
    [InlineData(60, 0, 0, 30)]      // Last 1 h    -> 30 s -> 120
    [InlineData(24 * 60, 0, 15, 0)] // Last 24 h   -> 15 min -> 96
    [InlineData(7 * 24 * 60, 2, 0, 0)]  // Last 7 d  -> 2 h ->  84
    [InlineData(30 * 24 * 60, 6, 0, 0)] // Last 30 d -> 6 h -> 120
    public void A_window_takes_the_narrowest_rung_that_keeps_it_readable(
        int windowMinutes, int hours, int minutes, int seconds)
    {
        var width = BucketWidth.For(TimeSpan.FromMinutes(windowMinutes));

        Assert.Equal(new TimeSpan(0, hours, minutes, seconds), width);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(15)]
    [InlineData(60)]
    [InlineData(24 * 60)]
    [InlineData(7 * 24 * 60)]
    [InlineData(30 * 24 * 60)]
    public void Every_preset_window_lands_within_half_the_target(int windowMinutes)
    {
        // The rungs are what bound how close a window can get, so the shortfall is worth pinning:
        // a preset that quietly halved its Bucket count would read as a coarser chart for no reason.
        var window = TimeSpan.FromMinutes(windowMinutes);

        Assert.InRange(window.Ticks / BucketWidth.For(window).Ticks, 60, 120);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(60)]
    [InlineData(45)] // Not a preset: a hand-typed Relative Range gets the same treatment.
    [InlineData(7 * 24 * 60)]
    [InlineData(365 * 24 * 60)]
    public void No_window_is_ever_cut_into_more_buckets_than_the_target(int windowMinutes)
    {
        var window = TimeSpan.FromMinutes(windowMinutes);

        Assert.True(window.Ticks / BucketWidth.For(window).Ticks <= 120);
    }

    [Fact]
    public void Past_the_top_rung_the_bucket_count_grows_rather_than_the_rung()
    {
        // A Time Filter bounding nothing over years of telemetry still has to terminate.
        var width = BucketWidth.For(TimeSpan.FromDays(10 * 365));

        Assert.Equal(TimeSpan.FromDays(30), width);
    }

    [Fact]
    public void Edges_fall_on_multiples_of_the_width_counted_in_utc()
    {
        var width = TimeSpan.FromMinutes(5);
        var instant = new DateTimeOffset(2026, 7, 29, 14, 03, 20, TimeSpan.Zero);

        Assert.Equal(
            new DateTimeOffset(2026, 7, 29, 14, 0, 0, TimeSpan.Zero),
            BucketWidth.Floor(instant, width));
    }

    [Fact]
    public void An_edge_is_the_same_edge_whichever_zone_the_instant_was_written_in()
    {
        var width = TimeSpan.FromHours(12);
        var utc = new DateTimeOffset(2026, 7, 29, 14, 03, 20, TimeSpan.Zero);
        var prague = utc.ToOffset(TimeSpan.FromHours(2));

        Assert.Equal(BucketWidth.Floor(utc, width), BucketWidth.Floor(prague, width));
    }

    [Fact]
    public void An_instant_sitting_exactly_on_an_edge_stays_where_it_is()
    {
        var width = TimeSpan.FromMinutes(1);
        var onTheEdge = new DateTimeOffset(2026, 7, 29, 14, 03, 0, TimeSpan.Zero);

        Assert.Equal(onTheEdge, BucketWidth.Floor(onTheEdge, width));
    }
}
