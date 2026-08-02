namespace Bugler.Exploration.Querying;

/// <summary>
/// How wide one Bucket of a Volume is. Picked from a fixed ladder rather than by dividing the
/// window into a fixed count: a divided window puts the edges on arbitrary instants and moves them
/// every time a Relative Range slides, so the whole chart re-bins several times a minute under Follow
/// and no bar can be compared with the one beside it. A ladder rung keeps its edges still (ADR 0003).
/// </summary>
public static class BucketWidth
{
    /// <summary>Roughly how many Buckets a window should come out as; the rung chosen never exceeds it.</summary>
    private const int Target = 120;

    private static readonly TimeSpan[] Ladder =
    [
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(1),
        // Without this rung a 7-day window falls from 168 Buckets straight to 56, less than half
        // the target — the ladder's own gaps, not the target, decide how close a window can get.
        TimeSpan.FromHours(2),
        TimeSpan.FromHours(3),
        TimeSpan.FromHours(6),
        TimeSpan.FromHours(12),
        TimeSpan.FromDays(1),
        TimeSpan.FromDays(7),
        // The top rung exists so a Time Filter that bounds nothing still terminates at a readable
        // number of Buckets; beyond it the count grows rather than the rung.
        TimeSpan.FromDays(30),
    ];

    /// <summary>The narrowest rung that fits <paramref name="window"/> into at most <see cref="Target"/> Buckets.</summary>
    public static TimeSpan For(TimeSpan window)
    {
        foreach (var rung in Ladder)
        {
            if (window.Ticks <= rung.Ticks * Target)
            {
                return rung;
            }
        }

        return Ladder[^1];
    }

    /// <summary>
    /// The narrowest rung no narrower than <paramref name="minimum"/> — for a caller that asked
    /// for "about N Buckets" of a window it names. Rounding lands on the ladder rather than on an
    /// arbitrary division, so the edges stay still (ADR 0003) and the reported width stays truthful.
    /// </summary>
    public static TimeSpan AtLeast(TimeSpan minimum)
    {
        foreach (var rung in Ladder)
        {
            if (minimum <= rung)
            {
                return rung;
            }
        }

        return Ladder[^1];
    }

    /// <summary>
    /// The start of the Bucket holding <paramref name="instant"/>. Edges fall on multiples of the
    /// width counted from the Unix epoch in UTC, never in the viewer's zone: a zone-aligned Bucket
    /// would be a calendar day, and daylight saving would hand one Bucket a year an extra hour of
    /// telemetry — a rendering artefact shaped exactly like a spike (ADR 0003).
    /// </summary>
    public static DateTimeOffset Floor(DateTimeOffset instant, TimeSpan width)
    {
        var offset = (instant - DateTimeOffset.UnixEpoch).Ticks;
        // Remainder, not division: C# truncates towards zero, which would round a pre-epoch
        // instant up into the Bucket after the one holding it.
        var remainder = offset % width.Ticks;
        if (remainder < 0)
        {
            remainder += width.Ticks;
        }

        return DateTimeOffset.UnixEpoch + TimeSpan.FromTicks(offset - remainder);
    }
}
