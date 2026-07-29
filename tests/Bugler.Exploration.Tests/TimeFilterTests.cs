using Bugler.Exploration.Querying;
using Npgsql;

namespace Bugler.Exploration.Tests;

public class TimeFilterTests
{
    [Theory]
    [InlineData("PT15M", 0, 0, 15)]
    [InlineData("PT1H", 0, 1, 0)]
    [InlineData("P7D", 7, 0, 0)]
    [InlineData("P30D", 30, 0, 0)]
    [InlineData("P1M", 30, 0, 0)] // xsd:duration counts a month as 30 days — the convention we chose.
    public void Relative_range_reads_an_iso_duration(string text, int days, int hours, int minutes)
    {
        Assert.True(RelativeRange.TryParse(text, null, out var range));
        Assert.Equal(new TimeSpan(days, hours, minutes, 0), range.Duration);
    }

    [Theory]
    [InlineData("15m")] // Prometheus shorthand is not our contract.
    [InlineData("PT0S")]
    [InlineData("-P1D")]
    [InlineData("2026-07-28T14:12:00Z")]
    [InlineData("")]
    public void Relative_range_refuses_anything_that_is_not_a_positive_duration(string text)
    {
        Assert.False(RelativeRange.TryParse(text, null, out _));
    }

    [Theory]
    [InlineData("2026-07-28T14:12:00Z", 14)]
    [InlineData("2026-07-28T16:12:00+02:00", 14)]
    [InlineData("2026-07-28T12:12:00-02:00", 14)]
    public void Range_bound_normalizes_an_offset_to_utc(string text, int expectedUtcHour)
    {
        Assert.True(RangeBound.TryParse(text, null, out var bound));
        Assert.Equal(TimeSpan.Zero, bound.Instant.Offset);
        Assert.Equal(expectedUtcHour, bound.Instant.Hour);
    }

    [Theory]
    [InlineData("2026-07-28T14:12:00")] // No offset: reading it as UTC would silently shift the window.
    [InlineData("2026-07-28")]
    [InlineData("yesterday")]
    [InlineData("")]
    public void Range_bound_refuses_an_instant_without_an_offset(string text)
    {
        Assert.False(RangeBound.TryParse(text, null, out _));
    }

    [Fact]
    public void A_relative_range_cannot_be_combined_with_an_absolute_one()
    {
        Assert.False(TryCreate(range: "PT1H", from: "2026-07-28T14:12:00Z", to: null, out var error));
        Assert.NotNull(error);

        Assert.False(TryCreate(range: "PT1H", from: null, to: "2026-07-28T14:12:00Z", out error));
        Assert.NotNull(error);
    }

    [Fact]
    public void An_absolute_range_that_ends_before_it_starts_is_refused()
    {
        Assert.False(TryCreate(
            range: null, from: "2026-07-28T18:00:00Z", to: "2026-07-28T09:00:00Z", out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void Either_end_of_an_absolute_range_may_stay_open()
    {
        Assert.True(TryCreate(range: null, from: "2026-07-28T14:12:00Z", to: null, out _));
        Assert.True(TryCreate(range: null, from: null, to: "2026-07-28T14:12:00Z", out _));
        Assert.True(TryCreate(range: null, from: null, to: null, out _));
    }

    [Fact]
    public void A_relative_range_only_lower_bounds_the_query()
    {
        var conditions = Conditions(range: "PT1H", from: null, to: null, out var command);

        Assert.Equal(["timestamp >= now() - @range"], conditions);
        Assert.Equal(TimeSpan.FromHours(1), command.Parameters["range"].Value);
    }

    [Fact]
    public void An_absolute_range_bounds_the_query_on_both_ends_inclusively()
    {
        var conditions = Conditions(
            range: null, from: "2026-07-28T09:00:00Z", to: "2026-07-28T18:00:00Z", out _);

        Assert.Equal(["timestamp >= @from", "timestamp <= @to"], conditions);
    }

    [Fact]
    public void An_absent_time_filter_constrains_nothing()
    {
        Assert.Empty(Conditions(range: null, from: null, to: null, out _));
    }

    private static bool TryCreate(string? range, string? from, string? to, out string? error) =>
        TimeFilter.TryCreate(ParseRange(range), ParseBound(from), ParseBound(to), out _, out error);

    private static List<string> Conditions(
        string? range, string? from, string? to, out NpgsqlCommand command)
    {
        Assert.True(TimeFilter.TryCreate(ParseRange(range), ParseBound(from), ParseBound(to), out var filter, out _));
        var conditions = new List<string>();
        command = new NpgsqlCommand();
        filter.AddConditions(conditions, command, "timestamp");
        return conditions;
    }

    private static RelativeRange? ParseRange(string? text) =>
        text is null ? null : RelativeRange.Parse(text, null);

    private static RangeBound? ParseBound(string? text) =>
        text is null ? null : RangeBound.Parse(text, null);
}
