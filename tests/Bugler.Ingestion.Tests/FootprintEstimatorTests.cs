using Bugler.Ingestion.ReportStorageFootprint;

namespace Bugler.Ingestion.Tests;

public class FootprintEstimatorTests
{
    private static readonly Guid A = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000000");
    private static readonly Guid B = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000000");

    private static FootprintEstimator.WindowSample Sample(long rows, long bytes) => new(rows, bytes);

    [Fact]
    public void With_no_window_the_split_is_row_proportional()
    {
        var estimates = FootprintEstimator.Estimate(
            tableBytes: 1_000,
            rowsByService: new Dictionary<Guid, long> { [A] = 750, [B] = 250 },
            windowByService: new Dictionary<Guid, FootprintEstimator.WindowSample>());

        Assert.Equal(750, estimates[A]);
        Assert.Equal(250, estimates[B]);
    }

    [Fact]
    public void A_service_with_wider_rows_takes_a_wider_share_of_the_same_row_count()
    {
        var estimates = FootprintEstimator.Estimate(
            tableBytes: 1_000,
            rowsByService: new Dictionary<Guid, long> { [A] = 100, [B] = 100 },
            windowByService: new Dictionary<Guid, FootprintEstimator.WindowSample>
            {
                [A] = Sample(rows: 10, bytes: 1_000),
                [B] = Sample(rows: 10, bytes: 3_000),
            });

        Assert.Equal(250, estimates[A]);
        Assert.Equal(750, estimates[B]);
    }

    [Fact]
    public void A_service_silent_over_the_window_weighs_in_at_the_sampled_average_width()
    {
        var estimates = FootprintEstimator.Estimate(
            tableBytes: 1_000,
            rowsByService: new Dictionary<Guid, long> { [A] = 100, [B] = 100 },
            windowByService: new Dictionary<Guid, FootprintEstimator.WindowSample>
            {
                [A] = Sample(rows: 10, bytes: 2_000),
            });

        Assert.Equal(500, estimates[A]);
        Assert.Equal(500, estimates[B]);
    }

    /// <summary>
    /// Rows a Deletion orphaned still hold their bytes until the purge reclaims them, so they
    /// keep their share instead of inflating everyone else's.
    /// </summary>
    [Fact]
    public void An_orphans_share_is_not_smeared_over_the_registered()
    {
        var estimates = FootprintEstimator.Estimate(
            tableBytes: 1_000,
            rowsByService: new Dictionary<Guid, long> { [A] = 500, [B] = 500 },
            windowByService: new Dictionary<Guid, FootprintEstimator.WindowSample>());

        Assert.Equal(500, estimates[A]);
    }

    [Fact]
    public void An_empty_table_estimates_nothing_and_divides_by_nothing()
    {
        var estimates = FootprintEstimator.Estimate(
            tableBytes: 0,
            rowsByService: new Dictionary<Guid, long>(),
            windowByService: new Dictionary<Guid, FootprintEstimator.WindowSample>());

        Assert.Empty(estimates);
    }

    [Fact]
    public void The_shares_sum_back_to_the_table_within_rounding()
    {
        var c = Guid.Parse("cccccccc-0000-0000-0000-000000000000");
        var estimates = FootprintEstimator.Estimate(
            tableBytes: 1_000_003,
            rowsByService: new Dictionary<Guid, long> { [A] = 313, [B] = 3_141, [c] = 27 },
            windowByService: new Dictionary<Guid, FootprintEstimator.WindowSample>
            {
                [A] = Sample(rows: 31, bytes: 29_527),
                [B] = Sample(rows: 271, bytes: 33_311),
            });

        Assert.InRange(estimates.Values.Sum(), 1_000_000, 1_000_006);
    }
}
