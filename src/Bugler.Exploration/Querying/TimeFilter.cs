using Npgsql;

namespace Bugler.Exploration.Querying;

/// <summary>
/// The Time Filter of a query: either a Relative Range or an Absolute Range, never both.
/// A Relative Range is resolved against the server clock and only lower-bounds the query — the top
/// stays open so telemetry stamped in the future by a skewed sender remains visible (ADR 0002).
/// </summary>
public sealed class TimeFilter
{
    public static readonly TimeFilter None = new(null, null, null);

    private readonly RelativeRange? _range;
    private readonly RangeBound? _from;
    private readonly RangeBound? _to;

    private TimeFilter(RelativeRange? range, RangeBound? from, RangeBound? to)
    {
        _range = range;
        _from = from;
        _to = to;
    }

    /// <summary>
    /// Validates the two mutually exclusive query forms. On failure <paramref name="error"/> carries
    /// the message the endpoint returns as 400 — an impossible window is a caller's mistake, not an
    /// empty result set, which in an observability tool reads as "nothing was ever ingested".
    /// </summary>
    public static bool TryCreate(
        RelativeRange? range, RangeBound? from, RangeBound? to, out TimeFilter filter, out string? error)
    {
        filter = None;
        error = null;

        if (range is not null && (from is not null || to is not null))
        {
            error = "Use either range or from/to, not both.";
            return false;
        }

        if (from is { } lower && to is { } upper && lower.Instant > upper.Instant)
        {
            error = "The range ends before it starts.";
            return false;
        }

        filter = new TimeFilter(range, from, to);
        return true;
    }

    /// <summary>
    /// The lower bound as an instant, with a Relative Range resolved against <paramref name="now"/>.
    /// Null when the filter leaves the bottom open, which only the caller can fill (ADR 0003).
    /// </summary>
    public DateTimeOffset? LowerBound(DateTimeOffset now) =>
        _range is { } relative ? now - relative.Duration : _from?.Instant;

    /// <summary>The upper bound as an instant, or null — a Relative Range never sets one (ADR 0002).</summary>
    public DateTimeOffset? UpperBound => _to?.Instant;

    /// <summary>
    /// Restates the filter with its Relative Range already resolved, so a query and the Resolved
    /// Window reported alongside it are the same window rather than two readings of the clock.
    /// </summary>
    public TimeFilter Pin(DateTimeOffset now) =>
        _range is null ? this : new TimeFilter(null, RangeBound.At(now - _range.Value.Duration), _to);

    /// <summary>Appends the bounds to a query over <paramref name="column"/> and binds their values.</summary>
    public void AddConditions(List<string> conditions, NpgsqlCommand command, string column)
    {
        if (_range is { } relative)
        {
            conditions.Add($"{column} >= now() - @range");
            command.Parameters.AddWithValue("range", relative.Duration);
        }

        if (_from is { } from)
        {
            conditions.Add($"{column} >= @from");
            command.Parameters.AddWithValue("from", from.Instant);
        }

        if (_to is { } to)
        {
            conditions.Add($"{column} <= @to");
            command.Parameters.AddWithValue("to", to.Instant);
        }
    }
}
