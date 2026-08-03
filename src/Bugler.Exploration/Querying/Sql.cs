using System.Text.Json;

namespace Bugler.Exploration.Querying;

internal static class Sql
{
    /// <summary>
    /// How long a query that aggregates over a whole window may run. Aggregating is far dearer than
    /// reading a page of the same rows, so the aggregate gives up while the page still answers.
    /// </summary>
    public const int AggregateTimeoutSeconds = 5;

    /// <summary>
    /// The Volume's own budget, far wider than <see cref="AggregateTimeoutSeconds"/>. Every other
    /// aggregate has a way out — the total counts to a cap and stops — but the Volume has to touch
    /// every Log Record in the window to know the height of a bar, and at a retention's width that
    /// is tens of millions of rows. Measured on 20 million: about two seconds for a day, three and
    /// a half for the lot, once the pages are warm. This buys the widest window room to finish
    /// rather than failing at five seconds and leaving a reader with no chart at all; the chart
    /// says it is working while it waits. Pre-aggregation is what would make the wait go away.
    /// </summary>
    public const int VolumeTimeoutSeconds = 30;

    /// <summary>Npgsql rejects non-UTC DateTimes for timestamptz; query-string values arrive Unspecified.</summary>
    public static DateTime EnsureUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => value,
    };

    /// <summary>
    /// Attributes as the write path stored them. The default depth of 64 is left as it is on
    /// purpose, not by omission: the OTLP parser refuses a message nested past 100 levels, and an
    /// attribute spends two of those on every level of JSON it produces, so the deepest one a sender
    /// can get in is around 48 and this limit never decides anything. Raising it would guard nothing
    /// and would say what Bugler does not mean — that nesting without end is ordinary, and that the
    /// cost of it belongs to whoever opens the page rather than whoever sent the telemetry.
    /// <c>NestedAttributeDepthTests</c> is what keeps the two ends from drifting into each other.
    /// </summary>
    public static JsonElement ParseJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    /// <summary>Escapes LIKE wildcards so user input matches literally (pair with ESCAPE '\').</summary>
    public static string EscapeLike(string value) =>
        value.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
}
