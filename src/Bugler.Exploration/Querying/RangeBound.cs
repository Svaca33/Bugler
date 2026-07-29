using System.Globalization;

namespace Bugler.Exploration.Querying;

/// <summary>
/// One end of an Absolute Range. The offset is mandatory: a bare "2026-07-28T14:12:00" would be
/// read as UTC and silently shift the window for anyone who wrote it in local time, so it is refused.
/// </summary>
public readonly record struct RangeBound : IParsable<RangeBound>
{
    private RangeBound(DateTimeOffset instant) => Instant = instant;

    /// <summary>The instant the text denotes, normalized to UTC.</summary>
    public DateTimeOffset Instant { get; }

    public static RangeBound Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out var result)
            ? result
            : throw new FormatException($"'{s}' is not an instant carrying a UTC offset.");

    public static bool TryParse(string? s, IFormatProvider? provider, out RangeBound result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(s))
        {
            return false;
        }

        // DateTime parsing reports Unspecified exactly when the text carries neither 'Z' nor an offset.
        if (!DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var probe) ||
            probe.Kind == DateTimeKind.Unspecified)
        {
            return false;
        }

        if (!DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var instant))
        {
            return false;
        }

        result = new RangeBound(instant.ToUniversalTime());
        return true;
    }

    public override string ToString() => Instant.ToString("O", CultureInfo.InvariantCulture);
}
