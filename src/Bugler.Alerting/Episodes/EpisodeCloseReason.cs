using System.Text.Json.Serialization;

namespace Bugler.Alerting.Episodes;

[JsonConverter(typeof(JsonStringEnumConverter<EpisodeCloseReason>))]
public enum EpisodeCloseReason : short
{
    /// <summary>The Quiet Window passed without a matching Log Record (see CONTEXT.md: Quieted).</summary>
    QuietWindow = 1,

    /// <summary>The Watch feeding the Episode was turned off while it was open (see CONTEXT.md: Muted).</summary>
    WatchOff = 2,

    /// <summary>A human Solved it while it was still open — the verdict ended the stretch by hand.</summary>
    Solved = 3,

    /// <summary>
    /// The Fingerprint Rule or the Episode Scope changed under it, leaving its kind of trouble in
    /// a partition nothing will report again (see CONTEXT.md: Muted). Displays as Muted, like
    /// every close that is neither Quieted nor Solved — but records the truth rather than
    /// claiming a Watch was switched off.
    /// </summary>
    Regrouped = 4,
}
