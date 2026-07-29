using System.Xml;

namespace Bugler.Exploration.Querying;

/// <summary>
/// A Relative Range: a fixed-length duration back from "now", written as an ISO-8601 duration
/// (PT15M, PT1H, P7D, P30D). A week is 7 days and a month 30 days, so no calendar arithmetic —
/// and therefore no time zone — ever enters the contract.
/// </summary>
public readonly record struct RelativeRange : IParsable<RelativeRange>
{
    private RelativeRange(TimeSpan duration) => Duration = duration;

    public TimeSpan Duration { get; }

    public static RelativeRange Parse(string s, IFormatProvider? provider) =>
        TryParse(s, provider, out var result)
            ? result
            : throw new FormatException($"'{s}' is not a positive ISO-8601 duration.");

    public static bool TryParse(string? s, IFormatProvider? provider, out RelativeRange result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(s))
        {
            return false;
        }

        TimeSpan duration;
        try
        {
            // xsd:duration conversion counts a month as 30 days and a year as 365 — our convention exactly.
            duration = XmlConvert.ToTimeSpan(s);
        }
        catch (Exception exception) when (exception is FormatException or OverflowException)
        {
            return false;
        }

        if (duration <= TimeSpan.Zero)
        {
            return false;
        }

        result = new RelativeRange(duration);
        return true;
    }

    public override string ToString() => XmlConvert.ToString(Duration);
}
